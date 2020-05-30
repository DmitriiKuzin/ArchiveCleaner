// ----------------------------------------------------------------------
// <copyright file="StockListMakeRequest.cs" company="Смарт-Ком">
//     Copyright statement. All right reserved
// </copyright>
// Дата создания: 9.4.2018 
// Проект: Mercury integration platform
// Версия: 3.0 (Refactoring)
// Автор: Василий Ермаков (EMail: vasiliy.ermakov@smart-com.su)
// ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Engine.Core.Execute;
using Engine.Workflow;
using Microsoft.Practices.Unity;
using Module.MercuryQueue.Model;
using Module.MercuryQueue.Model.Requests;
using Engine.Core.DDL;
using Module.MercuryQueue.Service;
using Module.MercuryQueue.Model.Persist;
using Engine.Core.Query;
using Engine.Core.Service;
using Engine.Storage;
using Engine.Model.Document;
using Engine.Model;

namespace Module.MercuryQueue.Workflow.Action.Mercury.CheckForCompletedOutbound {

    public class CheckForCompletedOutboundMakeRequest : MercuryContextStepAction {

        public const string ARG_PAGE_SIZE = "PageSize";
        public const string ARG_PAGE_COUNT = "PageCount";
        public const string ARG_PAGE_START = "PageStart";
        public const string ARG_PAGE_ENTERPRISE_ID = "EnterpriseId";
        public const string ARG_MERCURY_LOGIN = "MercuryLogin";
        public const string ARG_SUFFIX = "Suffix";
        public const string ARG_ITERATION = "Iteration";
        public const string ARG_BEGIN_DATE = "BeginDate";
        public const string ARG_END_DATE = "EndDate";     

        private IMercuryMessageBodyGenerator messageGenerator;
        private string operationName;
        private string mercuryLogin;

        /// <summary>
        /// Флаг автоматического сохранения стутуса выполнения
        /// </summary>
        private bool breakAutosave = false;

        public override bool IsAutoStatus() {
            return !breakAutosave;
        }

        protected override string GetStepId() {
            return "6.1";
        }

        public CheckForCompletedOutboundMakeRequest(IUnityContainer container, ProcessInfo processInfo, MercurySettings mercurySettings) : base(container, processInfo, mercurySettings) {
            AddArgument<int>(ARG_PAGE_SIZE, Direction.In, 50);
            AddArgument<int>(ARG_PAGE_COUNT, Direction.In, 10);
            AddArgument<int>(ARG_PAGE_START, Direction.In, 1);
            AddArgument<Guid>(ARG_PAGE_ENTERPRISE_ID, Direction.In);
            AddArgument<string>(ARG_MERCURY_LOGIN, Direction.In);
            AddArgument<string>(ARG_SUFFIX, Direction.In, string.Empty);
            AddArgument<int?>(ARG_ITERATION, Direction.In, null);
            AddArgument<DateTimeOffset?>(ARG_BEGIN_DATE, Direction.In, null);
            AddArgument<DateTimeOffset?>(ARG_END_DATE, Direction.In, null);
        }

        protected override bool ActionBody(BaseProcess action) {
            bool isProcessed = true;
            try {
                messageGenerator = GetService<IMercuryMessageBodyGenerator>();

                int pageSize = GetValue<int>(ARG_PAGE_SIZE);
                int pageCount = GetValue<int>(ARG_PAGE_COUNT);
                int pageStart = GetValue<int>(ARG_PAGE_START);
                Guid enterpriseId = GetValue<Guid>(ARG_PAGE_ENTERPRISE_ID);
                operationName = GetValue<string>(ARG_OPERATION_NAME);
                mercuryLogin = GetValue<string>(ARG_MERCURY_LOGIN);
                string suffix = GetValue<string>(ARG_SUFFIX);
                int? iteration = GetValue<int?>(ARG_ITERATION);

                DateTimeOffset? beginDate = GetValue<DateTimeOffset?>(ARG_BEGIN_DATE);
                DateTimeOffset? endDate = GetValue<DateTimeOffset?>(ARG_END_DATE);

                LocalContext.SaveLog($"{MoscowDateTimeOffset().ToString()} Начало формирования запросов в систему Меркурий.");

                #region Проверка параметров
                if (pageCount <= 0) {
                    isProcessed = false;
                    LocalContext.SaveLog($" Количество страниц должно быть > 0.");
                    AppendUserErrorInfo(Guid.Empty, null, UserErrorConsts.ET_VALIDATION, UserErrorConsts.StockList.RTE_MAKE_REQUEST, "Количество страниц должно быть > 0");
                }
                if (pageSize <= 0) {
                    isProcessed = false;
                    LocalContext.SaveLog($" Размер страниц должно быть > 0.");
                    AppendUserErrorInfo(Guid.Empty, null, UserErrorConsts.ET_VALIDATION, UserErrorConsts.StockList.RTE_MAKE_REQUEST, "Размер страниц должно быть > 0");
                }
                if (pageStart <= 0) {
                    isProcessed = false;
                    LocalContext.SaveLog($" Начальная страница должна быть > 0.");
                    AppendUserErrorInfo(Guid.Empty, null, UserErrorConsts.ET_VALIDATION, UserErrorConsts.StockList.RTE_MAKE_REQUEST, "Начальная страница должна быть > 0");
                }
                Document enterpriseDoc = LocalReader.LoadDocumentByType(enterpriseId, "Enterprise");

                string enterpriseGUID = (string) enterpriseDoc["GUID"];
                string enterpriseCode = (string) enterpriseDoc["Code"];

                if (enterpriseDoc == null) {
                    isProcessed = false;
                    AppendUserErrorInfo(Guid.Empty, null, UserErrorConsts.ET_VALIDATION, UserErrorConsts.StockList.VALID_NO_ENTERPRISE, $"Не найдена площадка с ИД {enterpriseId.ToString()}/{enterpriseId.ToStringNumber()}");
                    LocalContext.SaveLog($" Не найдена площадка с ИД {enterpriseId.ToString()}/{enterpriseId.ToStringNumber()}.");
                }
                #endregion

                if (isProcessed) {
                    List<StepContext> steps = new List<StepContext>();
                    LocalContext.SaveLog($" Площадка для формирования запросов {enterpriseCode}.");
                    LocalContext.SaveLog($" Стартовая страница {pageStart}, количество страниц для получения {pageCount}, размер страницы {pageSize}.");
                    for (int i = 0; i < pageCount; i++) {
                        LocalContext.SaveLog($" Формирование страницы {(i + pageStart - 1) * pageSize} - {(i + pageStart) * pageSize}.");

                        StepContext requestContext = CreateStep(QueryId, MQSOperationType.COMMUNICATE);
                        requestContext.MercuryQueueStep.Status = MQSStatus.NEW;
                        if (iteration.HasValue)
                            requestContext.MercuryQueueStep.StepId = "0.0_" + iteration.Value.ToString("D3");
                        else
                            requestContext.MercuryQueueStep.StepId = "0.0";
                        requestContext.MercuryQueueStep.StepNo = LocalContext.MercuryQueueStep.StepNo + 1;
                        requestContext.MercuryQueueStep.StepName = Consts.STEP_NAME_MERCURY_COMMUNICATE;
                        requestContext.MercuryQueueStep.Description = "Сформирован запрос";
                        requestContext.MercuryQueueStep.OperationGroup = Consts.OP_GROUP_CHECK_FOR_COMPLETED_OUTBOUND + suffix;
                        requestContext.GenerateFileNames(operationName);
                        var req = GenerateRequest(i + pageStart, pageSize, LocalContext.MercurySettings, mercuryLogin,
                            enterpriseGUID, enterpriseCode, beginDate, endDate);
                        requestContext.SaveRequest(req);
                        var args = new Dictionary<string, ActionArgument>();
                        args.Add("PageNo", new ActionArgument(typeof(int), "PageNo", i));
                        requestContext.MercuryQueueStep.Parameters = ArgumentSerializer.SerializeArguments(args);
                        steps.Add(requestContext);
                        LocalContext.SaveLog($" Страница {i + pageStart - 1} сформирована.");
                    }

                    // Сохранение информации о сгенерированных запросах 
                    using (IDatabaseConnector connector = GetService<IDatabaseConnector>()) {
                        IMercuryQueueStepService transactionPersist = this.GetService<IMercuryQueueStepService>(connector);
                        transactionPersist.RegisterSteps(steps);
                        transactionPersist.SaveFinish(LocalContext.MercuryQueueStep.Id, MQSStatus.COMPLETE, "Выполнено формирование запросов.", DateTimeOffset.UtcNow);
                        connector.Commit();
                        breakAutosave = true;
                        SetValue("Status", MQSStatus.COMPLETE);
                    }
                } else {
                    LocalContext.SaveLog($" Заданы не все параметры для формирования запроса(-ов).");
                    LocalContext.SaveLog($" Операция отменена.");
                    ResultDescription = "Ошибка. Не верные параметры.";
                }
            } catch (Exception e) {
                isProcessed = false;
                AppendUserErrorInfo(Guid.Empty, null, UserErrorConsts.ET_MIP, UserErrorConsts.StockList.RTE_MAKE_REQUEST, UserErrorConsts.DEFAULT_LAS_SUPPRT_ERROR);
                ResultDescription = "Ошибка процесса формирования запросов в систему 'Меркурий'.";
                LocalContext.SaveLog(e.ToString());
            }
            LocalContext.SaveLog($"{MoscowDateTimeOffset().ToString()} Окончание формирования запросов в систему 'Меркурий'.");
            return isProcessed;
        }

        /// <summary>
        /// Гененрирование XML запроса в меркурий
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name=ARG_PAGE_SIZE>Размер страницы</param>
        /// <param name="settings">Настройки</param>
        /// <returns></returns>
        private string GenerateRequest(int pageIndex, int pageSize, MercurySettings settings, string loginName, string enterpriseGUID, string enterpriseCode, DateTimeOffset? beginDate, DateTimeOffset? endDate) {
            int pageOffset = (pageIndex - 1) * pageSize;
            var model = new CheckForCompletedOutboundRequestModel();
            model.Offset = pageOffset;
            model.Count = pageSize;
            model.LocalTransactionId = Guid.NewGuid().ToString();
            model.Login = loginName;
            model.EnterpriseGuid = enterpriseGUID;
            model.EnterpriseCode = enterpriseCode;
            model.FillFromMercurySettings(settings);
            model.IssueDate = MoscowDateTimeOffset();
            model.ReceiptBeginDate = beginDate;
            model.ReceiptEndDate = endDate;
            return messageGenerator.GetMessageBody(Consts.MERCURY_CHECK_FOR_COMPLETED_OUTBOUND_OPERATION, settings, model);
        }

        protected override string GetStepName() {
            return nameof(CheckForCompletedOutboundMakeRequest);
        }

        protected override string GetOperationGroup() {
            return Consts.OP_GROUP_CHECK_FOR_COMPLETED_OUTBOUND;
        }
    }
}
