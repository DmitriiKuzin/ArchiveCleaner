// ----------------------------------------------------------------------
// <copyright file="MIPGetVetDocumentListV2Operation.cs" company="Смарт-Ком">
//     Copyright statement. All right reserved
// </copyright>
// Дата создания: 9.11.2017 
// Проект: Mercury integration platform
// Версия: 1.0
// Автор: Василий Ермаков (EMail: vasiliy.ermakov@smart-com.su)
// ------------------------------------------------------------------------

using Engine.Workflow;
using System;
using System.Collections.Generic;
using Engine.Core.Execute;
using Microsoft.Practices.Unity;
using Engine.Core.Service;
using Module.MercuryQueue.Workflow.Action.Mercury.VetDocumentList;
using Module.MercuryQueue.Workflow.Action;
using Module.MercuryQueue.Model;
using Module.MercuryQueue.Service;
using Module.MercuryQueue.Model.Persist;
using Module.MercuryQueue.Workflow.Action.Mercury.PrepareOutgoingConsignment;
using Engine.Core.Query;
using Engine.Storage;
using Engine.Model.Document;
using System.Linq;
using Module.MercuryQueue.Workflow.Action.Mercury.ResolveDiscrepancy;
using Module.MercuryQueue.Workflow.Action.MIP.GetStockList;
using Module.MIPMessageQueue.Workflow.Action;
using Module.MercuryQueue.Workflow.Action.MIP.TransportOperation;
using Engine.Core.DDL;
using Engine.Model.Metha;

namespace Module.MercuryQueue.Workflow.Process.Mercury {

    public class MercuryOutboundOperation : UserErrorMIPOperationProcess {

        private const string PARAM_RESOLVE_DISCREPANCY_IDS = "ResolveDiscrepancyIds";
        private const string PARAM_COPACKING_DISCREPANCY_IDS = "CopackingDiscrepancyIds";
        private const string PARAM_PROD_OPERATION_MARS_IDS = "ProdOperationMarsIds";
        private const string PARAM_PROD_OPERATION_MERC_IDS = "ProdOperationMercIds";
        private const string PARAM_UPDATED_STOCK_ENTRY_IDS = "UpdatedStockEntryIds";
        private const string PARAM_USER_ID = "UserId";
        private const string PARAM_ENTERPRISE_ID = "EnterpriseId";
        private const string PARAM_DELIVERY_ID = "DeliveryId";
        private const string OUTBOUND_DELIVERY_METHA_NAME = "OutboundDelivery";

        private UserErrorInfo globalErrorInfo = null;
        private UserErrorInfo laborResearchErrorInfo = null;

        public MercuryOutboundOperation(IUnityContainer container, ProcessInfo processInfo) : base(container, processInfo) {
            EnableService<IDocumentPersistService>();
            EnableService<IMercuryQueueService>();
            EnableService<IMercuryQueueStepService>();
            EnableService<IProcess>();
            EnableService<ISystemSettingManager>();
        }

        protected override bool ActionBody(BaseProcess thisAction) {
            string recoverActionName = string.Empty;
            bool isProcessed = true;
            IMercuryQueueService queueService = GetService<IMercuryQueueService>();
            IMercuryQueueStepService queueStepService = GetService<IMercuryQueueStepService>();

            int totalIteration = 1;
            int totalCopackingIteration = 1;
            int totalProductionIteration = 2;
            int totalCheckRegionalizationIteration = 1;
            IList<Guid> vdoIdsd = null;
            Guid deliveryId = Guid.Empty;
            try {
                // Загрузка информации по самому запросу
                MercuryQueueModel queue = queueService.LoadQueue(QueryId);
                if (queue != null) {

                    IEnumerable<Document> loadedVetDocuments = null;

                    // Загрузка последнего шага операции
                    MercuryQueueStepModel step = queueStepService.GetLastMercuryQueueStep(QueryId);
                    IDictionary<string, ActionArgument> parameters = ArgumentSerializer.DeSerializeArguments(queue.Params);
                    vdoIdsd = parameters["VetDocumentOutbounds"].GetValue<IList<Guid>>();

                    IAction action = null;
                    IProcess process = null;

                    ISystemSettingManager settingManager = GetService<ISystemSettingManager>();

                    string mercuryLogin = string.Empty;
                    // Опеределение логина пользователя Меркурия. Через привязку или смену
                    Guid enterpriseId = parameters[PARAM_ENTERPRISE_ID].GetValue<Guid>();
                    Guid userId = parameters[PARAM_USER_ID].GetValue<Guid>();
                    using (IAction getUserAction = GetService<IAction>(nameof(GetMercuryLoginAction))) {
                        getUserAction.SetValue(GetMercuryLoginAction.ARG_ENTERPRISE_IDS, new List<Guid>() { enterpriseId });
                        getUserAction.SetValue(GetMercuryLoginAction.ARG_OPERATION_NAME, Consts.MERCURY_USER_ROLE.TRANSPORT);
                        getUserAction.SetValue(GetMercuryLoginAction.ARG_USER_ID, userId);
                        if (getUserAction.Execute()) {
                            IDictionary<Guid, string> res = getUserAction.GetValue<IDictionary<Guid, string>>("Result");
                            if (res.ContainsKey(enterpriseId))
                                mercuryLogin = res[enterpriseId];
                        }
                    }

                    string checkParams = string.Empty;

                    if (!parameters.ContainsKey(PARAM_DELIVERY_ID) || parameters[PARAM_DELIVERY_ID].GetValue<Guid>() == Guid.Empty) {
                        checkParams += "Не передан ИД поставки по которой производится выписка ВСД." + Environment.NewLine;
                    } else {
                        deliveryId = parameters[PARAM_DELIVERY_ID].GetValue<Guid>();
                    }

                    // Проверка того что все параметры верные
                    if (!parameters.ContainsKey("VetDocumentOutbounds") || !parameters["VetDocumentOutbounds"].GetValue<IList<Guid>>().Any()) {
                        checkParams += "Не передано ни одного ИД ВСД для отправки в систему 'Меркурий'." + Environment.NewLine;
                    }
                    if (string.IsNullOrWhiteSpace(mercuryLogin)) {
                        checkParams += UserErrorConsts.DEFAULT_MERCURY_LOGIN_NOT_FOUND_ERROR;
                    }
                    // Заданы не все параметры
                    if (!string.IsNullOrWhiteSpace(checkParams)) {
                        string errorString = "Заданы не все параметры для выполнения процесса." + Environment.NewLine + checkParams;
                        globalErrorInfo = CreateUserErrorInfo(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, UserErrorConsts.ET_VALIDATION, UserErrorConsts.OutgoingConsignment.VALID_PROCESS_PARAMS, errorString);
                        // Обновление статуса и простановка в ошибку
                        string tempStr = UpdateOperationSatus(vdoIdsd, globalErrorInfo);
                        queueService.SaveError(QueryId, checkParams + Environment.NewLine + tempStr);
                        // Логирование сообщения для пользователя.
                        LogUserError(globalErrorInfo);
                        return false;
                    }

                    bool isUseRegionalization = settingManager.GetByPrefix("MIP_USE_REGIONALIZATION_FOR_OUTBOUND", true);

                    queueService.ChangeStatus(QueryId, MQSStatus.PROCESS.ToString());
                    SetDeliveryProcessSatus(deliveryId);
                    ResetUserErrors(deliveryId);

                    loadedVetDocuments = LoadVetDocumentOutbound(vdoIdsd);

                    // Обновление статуса на PROCESS
                    if (loadedVetDocuments.Any()) {
                        using (IDatabaseConnector connector = GetService<IDatabaseConnector>()) {
                            IDocumentPersistService persist = this.GetService<IDocumentPersistService>(connector);
                            List<Document> originalList = new List<Document>();
                            foreach (Document d in loadedVetDocuments.Where(d => d["Status"] as string != "COMPLETE")) {
                                originalList.Add(d.Clone());
                                d["Status"] = MQSStatus.PROCESS.ToString();
                            }
                            persist.UpdateDocument(originalList, loadedVetDocuments, false, ProcessInfo);
                            connector.Commit();
                        }
                    }

                    #region Точки восстановления

                    if (step != null) {
                        if (step.OperationGroup == Consts.OP_GROUP_LABORATORY_RESEARCH) {
                            recoverActionName = nameof(MercuryLaborResearch);
                            goto MercuryLaborResearch;
                        } else if (step.OperationGroup == Consts.OP_GROUP_RESOLVE_DISCREPANCY_COPACKING) {
                            recoverActionName = nameof(MercuryResolveDiscrepancyCopacking);
                            goto CopackingDiscrepancyCase;
                        } else if (step.OperationGroup == Consts.OP_GROUP_RESOLVE_DISCREPANCY) {
                            recoverActionName = nameof(MercuryResolveDiscrepancy);
                            goto ResolveDiscrepancyCase;
                        } else if (step.OperationGroup == Consts.OP_GROUP_CHECK_FOR_COMPLETED_OUTBOUND) {
                            recoverActionName = nameof(MercuryCheckForCompletedOutbound);
                            goto CheckCompletedOutbound;
                        } else if (step.OperationGroup == Consts.MERCURY_PRODUCTION_OPERATION) {
                            recoverActionName = nameof(MercuryProductionOperation);
                            goto AddProductionCase;
                        } else if (step.StepId == "0.0" && step.OperationGroup == Consts.OP_GROUP_PREPARE_OUTGOING_CONSIGNMENT) {
                            recoverActionName = nameof(MercuryCommunicateAction);
                            goto PrepareOutgoingConsignmentCommunicate;
                        } else if (step.StepId == "10.8" && step.Status != MQSStatus.COMPLETE) { // анализ ответа
                            recoverActionName = nameof(PrepareOutgoingConsignmentProcessResponse);
                            goto PrepareOutgoingConsignmentProcessResponse;
                        } else if ((step.StepId == "10.8" && step.Status == MQSStatus.COMPLETE) || (step.StepId == "10.9" && step.Status != MQSStatus.COMPLETE)) { // обновление StockEntryMarsBatch
                            recoverActionName = nameof(TransportOperationUpdateStockEntryMarsBatch);
                            goto TransportOperationUpdateStockEntryMarsBatch;
                        } else if ((step.StepId == "10.9" && step.Status == MQSStatus.COMPLETE) || (step.StepId == "10.10" && step.Status != MQSStatus.COMPLETE)) { // обновление StockEntryMarsBatch
                            recoverActionName = nameof(TransportOpertionCalculateStatus);
                            goto TransportOpertionCalculateStatus;
                        } else {
                            goto CheckRegionalization;
                        }
                    }
                    #endregion

                    #region Проверка регионализации
                    CheckRegionalization:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (isUseRegionalization) {

                        if (isProcessed) {
                            action = GetAction(nameof(PrepareOutgoingConsignmentPrepareCheckRegionalization));
                            action.SetValue(PrepareOutgoingConsignmentPrepareCheckRegionalization.ARG_DELIVERY_ID, deliveryId);
                            isProcessed = action.Execute();
                            SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                            bool skipRegionalizationProcess = action.GetValue<bool>(PrepareOutgoingConsignmentPrepareCheckRegionalization.ARG_SKIP_REGIONALIZATION_PROCESS);
                            if (isProcessed && skipRegionalizationProcess) { // Если успешно и свободный перевоз. Продолжить выписку.
                                goto ReExecuteOutgonigConsignment;
                            } else if (!isProcessed && action.GetValue<MQSStatus>(PrepareOutgoingConsignmentPrepareCheckRegionalization.ARG_STATUS) == MQSStatus.CONCURRENT) {
                                // Если ошибка конкуренции при сохранении сгенерированных запросов регионализации то повторить.
                                goto CheckRegionalization;
                            }
                        }
                        if (totalCheckRegionalizationIteration == 0) {
                            queueService.SaveError(QueryId, "Превышен лимит повторений проверок регионализации.");
                            isProcessed = false;
                            goto ExitProcess;
                        }

                    }

                    CheckShipmentRegionalizationProcess:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (isUseRegionalization) {
                        if (isProcessed) { // Если предыдущий не удачен либо запрет
                            IProcess regionalizationProcess = GetProcess(nameof(MercuryCheckShipmentRegionalization), "CheckRegionalization");
                            regionalizationProcess.SetValue(MercuryCheckShipmentRegionalization.ARG_DELIVERY_ID, deliveryId);
                            regionalizationProcess.SetValue(MercuryCheckShipmentRegionalization.ARG_OPERATION_NAME, Consts.QUEUE_CHECK_SHIPMENT_REGIONALIZATION);
                            isProcessed = regionalizationProcess.Execute();
                            if (!isProcessed) { // Если процесс регионализации не успешен то подтянуть пользовательские ошибки
                                globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, regionalizationProcess, "Ошибка при получении сведений о регионализации.",
                                    errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_REGIONALIZATION);
                            }
                        } else {
                            queueService.SaveError(QueryId, "Ошибка при получении сведений о регионализации.");
                            SetValue(ARG_STATUS, MQSStatus.ERROR);
                            globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при обработке ответов от ситемы 'Меркурий'.");
                            goto ExitProcess;
                        }

                        totalCheckRegionalizationIteration--;

                        if (isProcessed) { // Если регионализация завершилась успешно то произвести проверку
                            goto CheckRegionalization;
                        } else {
                            SetValue(ARG_STATUS, MQSStatus.ERROR);
                            queueService.SaveError(QueryId, "Ошибка при получении условий регионализации.");
                            goto ExitProcess;
                        }
                    }
                    #endregion

                    ReExecuteOutgonigConsignment:
                    if (isProcessed && NeedStopSteps()) return true;
                    #region Раздел выписки сертификатов в Меркурии
                    if (isProcessed) {
                        action = GetAction(nameof(PrepareOutgoingConsignmentFindStockEntries));
                        action.SetValue("VetDocumentOutbounds", loadedVetDocuments);
                        action.SetValue(PrepareOutgoingConsignmentFindStockEntries.ARG_DELIVERY_ID, deliveryId);
                        isProcessed = action.Execute();
                        SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                    }

                    PrepareOutgoingConsignmentCheckStockEntryVolume:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (isProcessed && (action.GetType().Name == nameof(PrepareOutgoingConsignmentFindStockEntries) || recoverActionName == nameof(PrepareOutgoingConsignmentCheckStockEntryVolume))) {
                        loadedVetDocuments = action.GetValue<IEnumerable<Document>>("VetDocumentOutbounds"); //из предыдущего
                        // Дальше Обработка результатов
                        action = GetAction(nameof(PrepareOutgoingConsignmentCheckStockEntryVolume));
                        action.SetValue("VetDocumentOutbounds", loadedVetDocuments);
                        action.SetValue("EnterpriseId", enterpriseId);
                        action.SetValue(PrepareOutgoingConsignmentCheckStockEntryVolume.ARG_DELIVERY_ID, deliveryId);
                        isProcessed = action.Execute();
                        SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                    } else {
                        queueService.SaveError(QueryId, "Ошибка при подборе складских записей для формирования запроса в Меркурий.");
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при подборе складских записей для формирования запроса в Меркурий.",
                            errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_FIND);
                        goto ExitProcess;
                    }

                    IEnumerable<StockAdjustModel> saModels = null;
                    IEnumerable<StockAdjustModel> saCopackingModels = null;
                    IEnumerable<StockAdjustModel> saProductionModels = null;
                    bool hasRaw = false;

                    if (isProcessed && action.GetType().Name == nameof(PrepareOutgoingConsignmentCheckStockEntryVolume)) {
                        loadedVetDocuments = action.GetValue<IEnumerable<Document>>("VetDocumentOutbounds"); //из предыдущего
                        saModels = action.GetValue<IEnumerable<StockAdjustModel>>(PrepareOutgoingConsignmentCheckStockEntryVolume.ARG_STOCK_ADJUST_MODELS);
                        saCopackingModels = action.GetValue<IEnumerable<StockAdjustModel>>(PrepareOutgoingConsignmentCheckStockEntryVolume.ARG_COPACKING_STOCK_ADJUST_MODELS);
                        saProductionModels = action.GetValue<IEnumerable<StockAdjustModel>>(PrepareOutgoingConsignmentCheckStockEntryVolume.ARG_PRODUCTION_STOCK_ADJUST_MODELS);
                        hasRaw = action.GetValue<bool>(PrepareOutgoingConsignmentCheckStockEntryVolume.ARG_HAS_RAW);
                    } else {
                        queueService.SaveError(QueryId, "Ошибка при определении достаточности объемов складских записей.");
                        if (!isProcessed && action.GetValue<MQSStatus>(PrepareOutgoingConsignmentCheckStockEntryVolume.ARG_STATUS) == MQSStatus.CONCURRENT) {
                            isProcessed = true; // Сброс ошибки чтобы зайти на повторение
                            recoverActionName = nameof(PrepareOutgoingConsignmentCheckStockEntryVolume);
                            // Если ошибка конкуренции при сохранении подобраных складских записей то повторить подбор. Может уже изменится объем.
                            goto PrepareOutgoingConsignmentCheckStockEntryVolume;
                        }
                        // Логировапние ошибок от шага
                        hasRaw = action.GetValue<bool>(PrepareOutgoingConsignmentCheckStockEntryVolume.ARG_HAS_RAW);
                        if (hasRaw) {
                            globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Недостаточно объемов на сырьевых складских записях.",
                            errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_RAW);
                        } else {
                            globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при подборе складских записей.",
                            errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_CHECK_VOLUME);
                        }
                        goto ExitProcess;
                    }

                    #region Если есть переупаковка
                    if (isProcessed && NeedStopSteps()) return true;
                    if (saCopackingModels != null && saCopackingModels.Any()) {
                        var isEnableCopackingDiscrepancy = settingManager.GetByPrefix("OUTBOUND_OPERATION_COPACKING_DISCREPANCY", false);
                        if (isEnableCopackingDiscrepancy) {
                            action = GetAction(nameof(PrepareOutgoingConsignmentPrepareCopackingDiscrepancy));
                            action.SetValue(PrepareOutgoingConsignmentPrepareCopackingDiscrepancy.ARG_STOCK_ADJUST_MODELS, saCopackingModels);
                            action.SetValue("OperationId", parameters["DocumentNum"].GetValue<string>());
                            action.SetValue(PrepareOutgoingConsignmentPrepareCopackingDiscrepancy.ARG_ENTERPRISE_ID, parameters["EnterpriseId"].GetValue<Guid>());
                            action.SetValue(PrepareOutgoingConsignmentPrepareCopackingDiscrepancy.ARG_MERCURY_LOGIN, mercuryLogin);
                            action.SetValue(PrepareOutgoingConsignmentPrepareCopackingDiscrepancy.ARG_DELIVERY_ID, deliveryId);
                            isProcessed = action.Execute();
                            if (isProcessed) {
                                IList<Guid> copackingDiscrepancyIds = action.GetValue<IList<Guid>>(PARAM_COPACKING_DISCREPANCY_IDS);
                                // Сохранение списка ID созданных запией  
                                if (!parameters.ContainsKey(PARAM_COPACKING_DISCREPANCY_IDS)) parameters.Add(PARAM_COPACKING_DISCREPANCY_IDS, new ActionArgument(typeof(IList<Guid>), PARAM_COPACKING_DISCREPANCY_IDS, Direction.InOut));
                                parameters[PARAM_COPACKING_DISCREPANCY_IDS].SetValue(copackingDiscrepancyIds);
                                queueService.SaveParameters(QueryId, ArgumentSerializer.SerializeArguments(parameters));

                                SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                                goto CopackingDiscrepancyCase;
                            } else {
                                globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при корреткировке складских записей Copacking.",
                            errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_COPACKING_PREPARE);
                                queueService.SaveError(QueryId, "Ошибка при корреткировке складских записей Copacking.");
                                goto ExitProcess;
                            }
                        } else {
                            isProcessed = false;
                            queueService.SaveError(QueryId, "Автоматическое поднятие складских записей Copacking в текущей реализации не поддерживается.");
                            goto ExitProcess;
                        }
                    }
                    #endregion

                    if (isProcessed && NeedStopSteps()) return true;
                    #region Если есть производственные записи
                    if (saProductionModels != null && saProductionModels.Any()) {
                        action = GetAction(nameof(PrepareOutgoingConsignmentPrepareAddProduction));
                        action.SetValue(PrepareOutgoingConsignmentPrepareAddProduction.ARG_STOCK_ADJUST_MODELS, saProductionModels);
                        action.SetValue(PrepareOutgoingConsignmentPrepareAddProduction.ARG_ENTERPRISE_ID, enterpriseId);
                        action.SetValue(PrepareOutgoingConsignmentPrepareAddProduction.ARG_DELIVERY_ID, deliveryId);

                        isProcessed = action.Execute();
                        if (isProcessed) {
                            IList<Guid> poMercIds = action.GetValue<IList<Guid>>(PrepareOutgoingConsignmentPrepareAddProduction.ARG_PRODOPERATION_MERC_IDS);
                            IList<Guid> poMarsIds = action.GetValue<IList<Guid>>(PrepareOutgoingConsignmentPrepareAddProduction.ARG_PRODOPERATION_MARS_IDS);
                            // Сохранение списка ID созданных запией  
                            if (!parameters.ContainsKey(PARAM_PROD_OPERATION_MERC_IDS)) parameters.Add(PARAM_PROD_OPERATION_MERC_IDS, new ActionArgument(typeof(IList<Guid>), PARAM_PROD_OPERATION_MERC_IDS, Direction.InOut));
                            parameters[PARAM_PROD_OPERATION_MERC_IDS].SetValue(poMercIds);

                            if (!parameters.ContainsKey(PARAM_PROD_OPERATION_MARS_IDS)) parameters.Add(PARAM_PROD_OPERATION_MARS_IDS, new ActionArgument(typeof(IList<Guid>), PARAM_PROD_OPERATION_MARS_IDS, Direction.InOut));
                            parameters[PARAM_PROD_OPERATION_MARS_IDS].SetValue(poMarsIds);

                            queueService.SaveParameters(QueryId, ArgumentSerializer.SerializeArguments(parameters));

                            SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                            goto AddProductionCase;
                        } else {
                            globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при корреткировке произодственных складских записей.",
                            errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_PROD_PREPARE);
                            queueService.SaveError(QueryId, "Ошибка при корреткировке произодственных складских записей.");
                            goto ExitProcess;
                        }
                    }
                    #endregion

                    if (isProcessed && NeedStopSteps()) return true;
                    #region Если есть автоматическое поднятие ГП
                    if (saModels != null && saModels.Any()) { // Если есть модели то произвести корректировку складских записей
                        action = GetAction(nameof(PrepareOutgoingConsignmentPrepareResolveDiscrepancy));
                        action.SetValue("VetDocumentOutbounds", loadedVetDocuments);
                        action.SetValue(PrepareOutgoingConsignmentPrepareResolveDiscrepancy.ARG_STOCK_ADJUST_MODELS, saModels);
                        action.SetValue("OperationId", parameters["DocumentNum"].GetValue<string>());
                        action.SetValue(PrepareOutgoingConsignmentPrepareResolveDiscrepancy.ARG_ENTERPRISE_ID, parameters["EnterpriseId"].GetValue<Guid>());
                        action.SetValue(PrepareOutgoingConsignmentPrepareResolveDiscrepancy.ARG_MERCURY_LOGIN, mercuryLogin);
                        action.SetValue(PrepareOutgoingConsignmentPrepareResolveDiscrepancy.ARG_DELIVERY_ID, deliveryId);
                        isProcessed = action.Execute();
                        if (isProcessed) {
                            IList<Guid> resolveDiscrepancyIds = action.GetValue<IList<Guid>>(PARAM_RESOLVE_DISCREPANCY_IDS);
                            // Сохранение списка ID созданных запией ResolveDiscrepancy
                            if (!parameters.ContainsKey(PARAM_RESOLVE_DISCREPANCY_IDS)) parameters.Add(PARAM_RESOLVE_DISCREPANCY_IDS, new ActionArgument(typeof(IList<Guid>), PARAM_RESOLVE_DISCREPANCY_IDS, Direction.InOut));
                            parameters[PARAM_RESOLVE_DISCREPANCY_IDS].SetValue(resolveDiscrepancyIds);
                            queueService.SaveParameters(QueryId, ArgumentSerializer.SerializeArguments(parameters));

                            SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                            goto ResolveDiscrepancyCase;
                        } else {
                            globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при корреткировке складских записей.",
                            errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_RESOLVE_PREPARE);
                            queueService.SaveError(QueryId, "Ошибка при корреткировке складских записей.");
                            goto ExitProcess;
                        }
                    }
                    #endregion
                    
                    CheckCompletedOutbound:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (isProcessed && (action?.GetType()?.Name == nameof(PrepareOutgoingConsignmentCheckStockEntryVolume) || nameof(MercuryCheckForCompletedOutbound) == recoverActionName))
                    {
                        IEnumerable<Guid> seIds = null;
                        if (loadedVetDocuments.All(a => a.nameValues.ContainsKey(SharedFields.VDO_STOCK_ENTRY_ID))) {
                            seIds = loadedVetDocuments.Where(w => w[SharedFields.VDO_STOCK_ENTRY_ID] != null).Select(s => (Guid) s[SharedFields.VDO_STOCK_ENTRY_ID]).ToList();
                        } else {
                            seIds = LoadStockEntryIds(loadedVetDocuments.Select(s => (string) s["SourceStockEntryGUID"]).Distinct());
                        } 
                        
                        var checkForCompletedOutboundProcess = nameof(MercuryCheckForCompletedOutbound);
                        
                        process = GetProcess(checkForCompletedOutboundProcess, "");
                        process.SetValue(MercuryCheckForCompletedOutbound.ARG_ENTERPRISE_ID, enterpriseId);
                        process.SetValue(MercuryCheckForCompletedOutbound.ARG_MERCURY_LOGIN, mercuryLogin);
                        process.SetValue(MercuryCheckForCompletedOutbound.ARG_DELIVERY_ID, deliveryId);
                        process.SetValue(MercuryCheckForCompletedOutbound.ARG_STOCK_ENTRY_IDS, seIds);
                        isProcessed = process.Execute();
                    }

                    MercuryLaborResearch:
                    if (isProcessed && NeedStopSteps()) return true;
                    #region Проверка и внесени лабораторных исследований
                    if (isProcessed && (process?.GetType()?.Name == nameof(MercuryCheckForCompletedOutbound) || nameof(MercuryLaborResearch) == recoverActionName)) {
                        process = GetProcess(nameof(MercuryLaborResearch), nameof(MercuryLaborResearch));
                        IEnumerable<Guid> seIds = null;
                        if (loadedVetDocuments.All(a => a.nameValues.ContainsKey(SharedFields.VDO_STOCK_ENTRY_ID))) {
                            seIds = loadedVetDocuments.Where(w => w[SharedFields.VDO_STOCK_ENTRY_ID] != null).Select(s => (Guid) s[SharedFields.VDO_STOCK_ENTRY_ID]).ToList();
                        } else {
                            seIds = LoadStockEntryIds(loadedVetDocuments.Select(s => (string) s["SourceStockEntryGUID"]).Distinct());
                        } 
                        process.SetValue(MercuryLaborResearch.ARG_LAB_RESEARCH_STOCK_ENTRY_IDS, seIds);
                        process.SetValue(MercuryLaborResearch.ARG_LINKED_DOCUMENT_ID, deliveryId);
                        process.SetValue(MercuryLaborResearch.ARG_LINKED_DOCUMENT_METHA_NAME, OUTBOUND_DELIVERY_METHA_NAME);
                        if (seIds.Any()) {
                            isProcessed = process.Execute();
                            var stopped = process.GetValue<bool>(Consts.STOP_QUEUE_STEPS_ARG);
                            if (stopped) return true;

                            if (isProcessed) {
                                // успешно внесены лабораторки
                            } else {
                                // внесение лабораторки провалилось
                                // В текущей реализации ничего не делать внести только ошибки пользователю
                                laborResearchErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, process, "Ошибка при внесении лаборатоных исследований.",
                                        errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_LABOR_RESEARCH);
                                isProcessed = true;
                            }
                        }
                    }
                    #endregion

                    PrepareOutgoingConsignmentMakeRequest:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (isProcessed && (
                            (process?.GetType()?.Name == nameof(MercuryLaborResearch)) ||
                            (action?.GetType()?.Name == nameof(PrepareOutgoingConsignmentCheckStockEntryVolume) ||
                            recoverActionName == nameof(PrepareOutgoingConsignmentMakeRequest)))) { // Иначе выполенение запроса в меркурий
                        action = GetAction(nameof(PrepareOutgoingConsignmentMakeRequest));
                        action.SetValue("DeliveryId", deliveryId);
                        action.SetValue("VetDocumentOutbounds", loadedVetDocuments);
                        action.SetValue("MercuryLogin", mercuryLogin);
                        isProcessed = action.Execute();
                        SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                    }


                    PrepareOutgoingConsignmentCommunicate:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (isProcessed && (action?.GetType()?.Name == nameof(PrepareOutgoingConsignmentMakeRequest) || nameof(MercuryCommunicateAction) == recoverActionName)) {
                        // Дальше Коммуникация с меркурием
                        action = GetAction(nameof(MercuryCommunicateAction));
                        action.SetValue("OperationGroup", Consts.OP_GROUP_PREPARE_OUTGOING_CONSIGNMENT);
                        action.SetValue(MercuryCommunicateAction.ARG_REF_DOCUMENT_IDS, new List<Guid> { deliveryId });
                        isProcessed = action.Execute();
                        SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                    } else {

                        if (!isProcessed && action.GetValue<MQSStatus>(PrepareOutgoingConsignmentMakeRequest.ARG_STATUS) == MQSStatus.CONCURRENT) {
                            isProcessed = true; // Сброс ошибки чтобы зайти на повторение
                            recoverActionName = nameof(PrepareOutgoingConsignmentMakeRequest);
                            // Если ошибка конкуренции при сохранении подобраных складских записей то повторить подбор. Может уже изменится объем.
                            goto PrepareOutgoingConsignmentMakeRequest;
                        }

                        queueService.SaveError(QueryId, "Ошибка при формировании запросов в Меркурий.");
                        // Логировапние ошибок от шага
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при формировании запросов в Меркурий.",
                                        errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_PREPARE);
                        goto ExitProcess;
                    }

                    PrepareOutgoingConsignmentProcessResponse:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (isProcessed && (action?.GetType()?.Name == nameof(MercuryCommunicateAction) || nameof(PrepareOutgoingConsignmentProcessResponse) == recoverActionName)) {
                        // Сохранить ошибки полученные при коммуникации
                        //if (action != null)
                        //    globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, UserErrorConsts.DEFAULT_MERCURY_API_ERROR,
                        //                errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_COMM);

                        action = GetAction(nameof(PrepareOutgoingConsignmentProcessResponse));
                        isProcessed = action.Execute();
                        if (isProcessed) { // Сохранение параметров
                            IList<Guid> updatedStockEntryIds = action.GetValue<IList<Guid>>(PrepareOutgoingConsignmentProcessResponse.ARG_UPDATED_STOCK_ENTRY_IDS);
                            // Сохранение списка ID созданных/обновленных запией UpdatedStockEntry
                            if (!parameters.ContainsKey(PARAM_UPDATED_STOCK_ENTRY_IDS)) parameters.Add(PARAM_UPDATED_STOCK_ENTRY_IDS, new ActionArgument(typeof(IList<Guid>), PARAM_UPDATED_STOCK_ENTRY_IDS, Direction.InOut));
                            parameters[PARAM_UPDATED_STOCK_ENTRY_IDS].SetValue(updatedStockEntryIds);
                            queueService.SaveParameters(QueryId, ArgumentSerializer.SerializeArguments(parameters));
                        }
                        SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                    } else {
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, UserErrorConsts.DEFAULT_MERCURY_API_ERROR, UserErrorConsts.ET_COMMUNICATE, UserErrorConsts.OutgoingConsignment.GLOBAL_COMM);
                        queueService.SaveError(QueryId, "Ошибка при коммуникации с системой 'Меркурий'.");
                        goto ExitProcess;
                    }

                    TransportOperationUpdateStockEntryMarsBatch:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (isProcessed && (action?.GetType()?.Name == nameof(PrepareOutgoingConsignmentProcessResponse) || nameof(TransportOperationUpdateStockEntryMarsBatch) == recoverActionName)) {
                        // Обновление марс партий по полученным складским записям
                        //action = GetAction(nameof(TransportOperaton));
                        action = GetAction(nameof(TransportOperationUpdateStockEntryMarsBatch));
                        action.SetValue(TransportOperationUpdateStockEntryMarsBatch.ARG_UPDATED_STOCK_ENTRY_IDS, parameters[PARAM_UPDATED_STOCK_ENTRY_IDS].GetValue<IList<Guid>>());
                        action.SetValue(TransportOperationUpdateStockEntryMarsBatch.ARG_VET_DOCUMENT_OUTBOUND_IDS, parameters["VetDocumentOutbounds"].GetValue<IList<Guid>>());
                        action.SetValue(TransportOperationUpdateStockEntryMarsBatch.ARG_VET_DOCUMENT_OUTBOUNDS, null);
                        action.SetValue("EnterpriseId", parameters[PARAM_ENTERPRISE_ID].GetValue<Guid>());
                        action.SetValue("UserId", parameters[PARAM_USER_ID].GetValue<Guid>());
                        action.SetValue("DeliveryId", deliveryId);
                        action.SetValue("DocumentNum", parameters["DocumentNum"].GetValue<string>());
                        isProcessed = action.Execute();
                        SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
                    } else {
                        if (!isProcessed
                            && action?.GetType()?.Name == nameof(PrepareOutgoingConsignmentProcessResponse)
                            && action.GetValue<MQSStatus>(PrepareOutgoingConsignmentCheckStockEntryVolume.ARG_STATUS) == MQSStatus.CONCURRENT) {
                            isProcessed = true; // Сброс ошибки чтобы зайти на повторение
                            recoverActionName = nameof(PrepareOutgoingConsignmentProcessResponse);
                            // Если ошибка конкуренции при сохранении подобраных складских записей то повторить подбор. Может уже изменится объем.
                            goto PrepareOutgoingConsignmentProcessResponse;
                        }
                        queueService.SaveError(QueryId, "Ошибка при анализе ответа от Меркурия.");
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при обработке ответов от системы 'Меркурий'.",
                                        errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_RESPONSE);
                        goto ExitProcess;
                    }

                    TransportOpertionCalculateStatus: 
                    if (isProcessed && (action?.GetType()?.Name == nameof(TransportOperationUpdateStockEntryMarsBatch) || nameof(TransportOpertionCalculateStatus) == recoverActionName)) {
                        action = GetAction(nameof(TransportOpertionCalculateStatus));
                        action.SetValue(TransportOpertionCalculateStatus.ARG_DELIVERY_ID, deliveryId);
                        isProcessed = action.Execute();
                        SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));

                    } else {
                        if (!isProcessed
                            && action?.GetType()?.Name == nameof(TransportOperationUpdateStockEntryMarsBatch)
                            && action.GetValue<MQSStatus>(TransportOperationUpdateStockEntryMarsBatch.ARG_STATUS) == MQSStatus.CONCURRENT) {
                            isProcessed = true; // Сброс ошибки чтобы зайти на повторение
                            recoverActionName = nameof(TransportOperationUpdateStockEntryMarsBatch);
                            // Если ошибка конкуренции при сохранении подобраных складских записей то повторить подбор. Может уже изменится объем.
                            goto TransportOperationUpdateStockEntryMarsBatch;
                        }
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при обновлении складских записей. Обратитесь в поддержку.",
                                        errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_SEMB);
                        queueService.SaveError(QueryId, "Ошибка при обновлении складских записей.");
                        goto ExitProcess;
                    }

                    if (isProcessed && action?.GetType()?.Name == nameof(TransportOpertionCalculateStatus)) {
                        goto ExitProcess;
                    } else {
                        if (!isProcessed
                            && action?.GetType()?.Name == nameof(TransportOpertionCalculateStatus)
                            && action.GetValue<MQSStatus>(TransportOpertionCalculateStatus.ARG_STATUS) == MQSStatus.CONCURRENT) {
                            isProcessed = true; // Сброс ошибки чтобы зайти на повторение
                            recoverActionName = nameof(TransportOpertionCalculateStatus);
                            // Если ошибка конкуренции при сохранении подобраных складских записей то повторить подбор. Может уже изменится объем.
                            goto TransportOpertionCalculateStatus;
                        }
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Ошибка при определении статуса исходящей поставки.",
                                        errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_STATUS);
                        queueService.SaveError(QueryId, "Ошибка при определении статуса исходящей поставки.");
                        goto ExitProcess;
                    }

                    #endregion

                    ResolveDiscrepancyCase:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (totalIteration == 0) {
                        queueService.SaveError(QueryId, "Превышен лимит повторений ResolveDiscrepancy.");
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Превышен лимит повторений ResolveDiscrepancy.",
                                         errorType: UserErrorConsts.ET_MIP, errorCode: UserErrorConsts.OutgoingConsignment.RTE_RESOLVE_REPEAT);
                        isProcessed = false;
                        goto ExitProcess;
                    }
                    totalIteration--;

                    #region Раздел автоматической корректировки складских записей

                    if (isProcessed && (action?.GetType()?.Name == nameof(PrepareOutgoingConsignmentPrepareResolveDiscrepancy) || nameof(MercuryResolveDiscrepancy) == recoverActionName)) {
                        IProcess rdProcess = GetProcess(nameof(MercuryResolveDiscrepancy), nameof(MercuryResolveDiscrepancy));
                        rdProcess.SetValue(MercuryResolveDiscrepancy.ARG_MERCURY_LOGIN, mercuryLogin); 
                        isProcessed = rdProcess.Execute();
                        var stopped = rdProcess.GetValue<bool>(Consts.STOP_QUEUE_STEPS_ARG);
                        if (stopped) return true;
                        MQSStatus status = rdProcess.GetValue<MQSStatus>(MercuryResolveDiscrepancy.ARG_STATUS);

                        if (isProcessed) {
                            // Поднятие прошло успешно повтор выписки
                            goto ReExecuteOutgonigConsignment;
                        } else {
                            //queueService.SaveError(QueryId, "Ошибка при поднятии стока по Copacking.");
                            globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, rdProcess, "Ошибка при корреткировке складских записей Copacking.");
                            goto ExitProcess;
                        }
                    }



                    #endregion

                    CopackingDiscrepancyCase:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (totalCopackingIteration < 1) {
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Превышен лимит повторений CopackingDiscrepancy.",
                                        errorType: UserErrorConsts.ET_MIP, errorCode: UserErrorConsts.OutgoingConsignment.RTE_COPACKING_REPEAT);
                        queueService.SaveError(QueryId, "Превышен лимит повторений CopackingDiscrepancy.");
                        isProcessed = false;
                        goto ExitProcess;
                    }
                    totalCopackingIteration--;
                    {
                        if (isProcessed && (action?.GetType()?.Name == nameof(PrepareOutgoingConsignmentPrepareCopackingDiscrepancy) || nameof(MercuryResolveDiscrepancyCopacking) == recoverActionName)) {
                            IProcess copackingProcess = GetProcess(nameof(MercuryResolveDiscrepancyCopacking), nameof(MercuryResolveDiscrepancyCopacking));
                            copackingProcess.SetValue(MercuryResolveDiscrepancyCopacking.ARG_MERCURY_LOGIN, mercuryLogin); 
                            isProcessed = copackingProcess.Execute();
                            var stopped = copackingProcess.GetValue<bool>(Consts.STOP_QUEUE_STEPS_ARG);
                            if (stopped) return true;
                            MQSStatus status = copackingProcess.GetValue<MQSStatus>(MercuryResolveDiscrepancyCopacking.ARG_STATUS);

                            if (isProcessed) {
                                // Поднятие прошло успешно повтор выписки
                                goto ReExecuteOutgonigConsignment;
                            } else {
                                globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, copackingProcess, "Ошибка при поднятии складских записей. Обратитесь в поддержку.",
                                        errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_COPACKING);
                                goto ExitProcess;
                            }
                        }
                    }

                    AddProductionCase:
                    if (isProcessed && NeedStopSteps()) return true;
                    if (totalProductionIteration < 1) {
                        globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, action, "Превышен лимит повторений AddProduction.",
                                        errorType: UserErrorConsts.ET_MIP, errorCode: UserErrorConsts.OutgoingConsignment.RTE_PROD_REPEAT);
                        queueService.SaveError(QueryId, "Превышен лимит повторений AddProduction.");
                        isProcessed = false;
                        goto ExitProcess;
                    }
                    totalProductionIteration--;
                    {
                        if (isProcessed && (action?.GetType()?.Name == nameof(PrepareOutgoingConsignmentPrepareAddProduction) || nameof(MercuryProductionOperation) == recoverActionName)) {
                            IProcess prodProcess = GetProcess(nameof(MercuryProductionOperation), nameof(MercuryProductionOperation));
                            prodProcess.SetValue(MercuryProductionOperation.ARG_MERCURY_LOGIN, mercuryLogin);
                            isProcessed = prodProcess.Execute();

                            var stopped = prodProcess.GetValue<bool>(Consts.STOP_QUEUE_STEPS_ARG);
                            if (stopped) return true;

                            MQSStatus status = prodProcess.GetValue<MQSStatus>(MercuryProductionOperation.ARG_STATUS);

                            if (isProcessed) {
                                // Поднятие прошло успешно повтор выписки
                                goto ReExecuteOutgonigConsignment;
                            } else {
                                globalErrorInfo = LogStepUserErrors(deliveryId, OUTBOUND_DELIVERY_METHA_NAME, prodProcess, "Ошибка при корреткировке складских записей по незавершенному производству.",
                                        errorCode: UserErrorConsts.OutgoingConsignment.GLOBAL_PROD);
                                queueService.SaveError(QueryId, "Ошибка при поднятии стока по незавершенному производству.");
                                goto ExitProcess;
                            }
                        }
                    }

                    ExitProcess: 
                    if (isProcessed && NeedStopSteps()) return true;

                    if (isProcessed) {
                        isProcessed = PrepareSendDELIVERYEVCS(deliveryId);
                        if (!isProcessed) {
                            queueService.SaveError(QueryId, "Ошибка при подготовке к отправке сообщения DELIVERYEVCS.");
                        }
                    }

                    if (isProcessed) {
                        queueService.SaveResult(QueryId, "Операция выполнена успешно");
                    } else if (!isProcessed && action?.GetType()?.Name == nameof(VetDocumentListProcessResponse)) {
                        queueService.SaveError(QueryId, "Ошибка при обработке ответа от Меркурия");
                    } // Иначе будет залогировано уже ранее.

                    if (!isProcessed) {
                        #region Обновление статуса Error
                        UpdateOperationSatus(vdoIdsd, globalErrorInfo ?? laborResearchErrorInfo);
                        #endregion
                    }
                }
            } catch (Exception e) {
                if (NeedStopSteps()) return true;
                string tempStr = string.Empty;
                if (vdoIdsd != null && vdoIdsd.Any())
                    tempStr = UpdateOperationSatus(vdoIdsd, globalErrorInfo ?? laborResearchErrorInfo);
                queueService.SaveError(QueryId, e.ToString() + Environment.NewLine + tempStr);
                isProcessed = false;
            }

            return isProcessed;
        }

        /// <summary>
        /// Определение необходимости отправки DELIVERYEVCS сообщения для клиента по исходящей поставке
        /// </summary>
        /// <param name="delivery">Исходящая поставка</param>
        /// <returns>true - необходимо формировать DELIVERYEVCS, false - не нужно формировать DELIVERYEVCS</returns>
        private bool NeedToPrepareSendDELIVERYEVCS(Document delivery) {
            if (delivery == null)
                throw new ArgumentNullException("delivery");

            Guid customerId = (Guid) (delivery["CustomerId"] ?? Guid.Empty);
            Guid shipToId = (Guid) (delivery["ShipToId"] ?? Guid.Empty);

            if (customerId == Guid.Empty) return false;

            DocumentQuery query = new DocumentQuery(GetService<IMethaStorage>(), GetService<IQueryGenerator>(), GetService<ICacheManager>())
                .ResultField("D", "EntityId")
                .ResultField("D", "EnterpriseId")
                .Using("DeliveryEvcCustomers", "D", ObjectStatus.osActive)
                .Order("EnterpriseId", SortOrder.Descending)
                .Take(1);

            var where = new QueryNode() { Operator = QueryNodeOperator.AND };
            var entityNode = new QueryNode() { Operator = QueryNodeOperator.AND };
            entityNode.Rules.Add(new QueryRule() {
                LeftField = "EntityId",
                Operator = RuleOperator.Equals,
                ValueType = QueryRuleValueType.Guid,
                RightFieldValue = customerId
            });
            var enterpriseNode = new QueryNode() { Operator = QueryNodeOperator.OR };
            enterpriseNode.Rules.Add(new QueryRule() {
                LeftField = "EnterpriseId",
                Operator = RuleOperator.IsNull,
                ValueType = QueryRuleValueType.Guid,
                RightFieldValue = null
            });
            if (shipToId != Guid.Empty) {
                enterpriseNode.Rules.Add(new QueryRule() {
                    LeftField = "EnterpriseId",
                    Operator = RuleOperator.Equals,
                    ValueType = QueryRuleValueType.Guid,
                    RightFieldValue = shipToId
                });
            }

            where.Nodes.Add(entityNode);
            where.Nodes.Add(enterpriseNode);
            query.Where(where);

            return LocalReader.QueryDocuments(query).Result.Any();
        }

        /// <summary>
        /// Определение необходимости отправки сообщения DELIVERYEVCS клиенту и создание прототипа IDoc в таблице XMLStore
        /// </summary>
        /// <remarks>
        /// Необходимость определяется на основании таблицы DeliveryEvcCustomers по следующим условиям:
        /// 1. Если в DeliveryEvcCustomers для клиента есть запись без указания площадки, то для всех исходящих поставок на все площадки клиента будет сформировано сообщение DELIVERYEVCS.
        /// 2. Если в DeliveryEvcCustomers для клиента есть запись с указанием конкретной площадки, то при исходящей поставке только на эту площадку будет сформировано сообщение DELIVERYEVCS.
        /// </remarks>
        /// <param name="deliveryId">ID исходящей поставки.</param>
        /// <returns>true - обработано успешно, false - неуспешно</returns>
        private bool PrepareSendDELIVERYEVCS(Guid deliveryId) {
            if (deliveryId == null)
                throw new ArgumentNullException("deliveryId");

            bool result = true;
            Document deliveryDoc = LocalReader.LoadDocumentByType(deliveryId, "OutboundDelivery");

            if (NeedToPrepareSendDELIVERYEVCS(deliveryDoc)) {
                var action = GetAction(nameof(PrepareSendDELIVERYEVCSAction));
                action.SetValue(PrepareSendDELIVERYEVCSAction.ARG_DELIVERY, deliveryDoc);
                result = action.Execute();
                SetValue(ARG_STATUS, action.GetValue<MQSStatus>(ARG_STATUS));
            }

            return result;
        }

        private string UpdateOperationSatus(IEnumerable<Guid> vdoIdsd, UserErrorInfo errorInfo) {
            string result = string.Empty;
            try {
                IEnumerable<Document> vdoDocs = LocalReader.LoadDocumentsByType(vdoIdsd, "VetDocumentOutbound");
                IList<Document> odiDocs = LocalReader.QueryDocuments(this.CreateDocumentQuery()
                    .ResultField("X", "*")
                    .Using("OutboundDeliveryItem", "X")
                    .Where(w => w.Or(o => {
                        foreach (Guid g in vdoIdsd)
                            o.EQ("VetDocumentOutboundId", g, "X");
                    }))).Result;
                List<Guid> deliveryIds = odiDocs.Select(s => (Guid) s["DeliveryId"]).Distinct().ToList();
                IEnumerable<Document> odDocs = LocalReader.LoadDocumentsByType(deliveryIds, "OutboundDelivery");
                List<Document> originalDocs = new List<Document>();
                List<Document> updatedDocs = new List<Document>();
                // Обновление статуса выписываеиого ВСД
                if (vdoDocs != null) {
                    foreach (Document doc in vdoDocs) {
                        originalDocs.Add(doc.Clone());
                        doc["Status"] = MQSStatus.ERROR.ToString();
                        UpdateErrorFields(doc, errorInfo);
                        updatedDocs.Add(doc);
                    }
                }
                // Обновление статуса поставки
                if (odDocs != null) {
                    foreach (Document doc in odDocs) {
                        originalDocs.Add(doc.Clone());
                        doc["Status"] = MQSStatus.ERROR.ToString();
                        UpdateErrorFields(doc, errorInfo);
                        updatedDocs.Add(doc);
                    }
                }
                if (updatedDocs.Count > 0) {
                    using (IDatabaseConnector connector = GetService<IDatabaseConnector>()) {
                        IDocumentPersistService persist = this.GetService<IDocumentPersistService>(connector);
                        persist.UpdateDocument(originalDocs, updatedDocs, false, ProcessInfo);
                        connector.Commit();
                    }
                }

            } catch (Exception e) {
                result = e.ToString();
            }
            return result;
        }

        /// <summary>
        /// Сбросить поля ошибок при начале выполенеия процесса
        /// </summary>
        /// <param name="deliveryId"></param>
        private void SetDeliveryProcessSatus(Guid deliveryId) {
            Document odDoc = LocalReader.LoadDocumentByType(deliveryId, "OutboundDelivery");
            List<Document> originalDocs = new List<Document>();
            List<Document> updatedDocs = new List<Document>();
            if (odDoc != null) {
                originalDocs.Add(odDoc.Clone());
                odDoc["Status"] = MQSStatus.PROCESS.ToString();
                odDoc[Consts.DEFAULT_ERROR_CODE_FIELD] = null;
                odDoc[Consts.DEFAULT_ERROR_MESSAGE_FIELD] = null;
                updatedDocs.Add(odDoc);

                using (IDatabaseConnector connector = GetService<IDatabaseConnector>(Engine.Consts.WITHOUT_TRANSACTION)) {
                    IDocumentPersistService persist = this.GetService<IDocumentPersistService>(connector);
                    persist.UpdateDocument(originalDocs, updatedDocs, false, ProcessInfo);
                }
            }
        }

        /// <summary>
        /// Загружает документы по ИД 
        /// </summary>
        /// <param name="ids">список ИД документов VetDocumentOutbound</param>
        /// <returns></returns>
        private IEnumerable<Document> LoadVetDocumentOutbound(IEnumerable<Guid> ids) {
            List<Document> vetDocs = new List<Document>();
            IMethaStorage storage = GetService<IMethaStorage>();
            DocumentQuery query = new DocumentQuery(storage, GetService<IQueryGenerator>(), GetService<ICacheManager>())
                .ResultField("VDO", "*")
                .ResultField("ODI", "PositionNumber")
                .Using("VetDocumentOutbound", "VDO")
                .Using("OutboundDeliveryItem", "ODI")
                .Link("VDO", "ODI", "@Id", "VetDocumentOutboundId");

            foreach (var batch in ids.Batch(50)) {
                query.Where(w => w.Or(o => {
                    foreach (Guid g in batch.ToList())
                        o.EQ("@Id", g, "VDO");
                }));
                vetDocs.AddRange(LocalReader.QueryDocuments(query).Result);
            }
            MethaName mn = storage.GetMethaName("VetDocumentOutbound");
            vetDocs.ForEach(d => d.Metha_Id = mn.Metha_Id);
            return vetDocs;
        }

        private IList<Guid> LoadStockEntryIds(IEnumerable<string> seGUIDs) {
            List<Guid> result = new List<Guid>();
            DocumentQuery query = new DocumentQuery(GetService<IMethaStorage>(), GetService<IQueryGenerator>(), GetService<ICacheManager>())
                .ResultField("X", "*")
                .Using("StockEntry", "X");
            foreach (var batch in seGUIDs.Batch(50)) {
                query.Where(w => w.Or(o => {
                    foreach (string g in batch.ToList())
                        o.EQ("GUID", g, "X");
                }));
                result.AddRange(LocalReader.QueryDocuments(query).Result.Select(sm => sm.Doc_Id));
            }
            return result;
        }

        /// <summary>
        /// Получение действие шага
        /// </summary>
        /// <param name="name">Наименование шага</param>
        /// <returns></returns>
        private IAction GetAction(string name) {
            IAction result = GetService<IAction>(name);
            if (result.HasArgument("QueryId")) result.AssignArgument(this, "QueryId");
            if (result.HasArgument("CreateDate")) result.AssignArgument(this, "CreateDate");
            if (result.HasArgument("OperationName")) result.SetValue("OperationName", GetOperationName());
            return result;
        }

        /// <summary>
        /// Получение действие шага
        /// </summary>
        /// <param name="name">Наименование шага</param>
        /// <returns></returns>
        private IProcess GetProcess(string name, string subProcessName) {
            IProcess result;
            if (!string.IsNullOrWhiteSpace(subProcessName)) {
                ProcessInfo newPi = ProcessInfo.Clone();
                newPi.ProcessName += $" ({subProcessName})";
                result = GetService<IProcess>(name, new Dictionary<string, Object>() { ["ProcessInfo"] = newPi });
            } else {
                result = GetService<IProcess>(name);
            }
            if (result.HasArgument("QueryId")) result.AssignArgument(this, "QueryId");
            if (result.HasArgument("CreateDate")) result.AssignArgument(this, "CreateDate");
            if (result.HasArgument("OperationName")) result.SetValue("OperationName", GetOperationName());
            if (result.HasArgument("IsSelfProcess")) result.SetValue("IsSelfProcess", false); 
            return result;
        }

        protected override string GetOperationName() {
            return Consts.QUEUE_OUTBOUND_OPERATION;
        }
    }
}
