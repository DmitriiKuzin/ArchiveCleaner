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
using MercuryNS = Module.MercuryQueue.MercuryModel;
using Module.MercuryQueue.Parsers;
using Module.MercuryQueue.Helpers;
using Engine.Model.Document;
using Engine.Core.Service;
using Engine.Storage;
using Engine.Core.Query;
using System.IO;
using Engine.Model.Metha;

namespace Module.MercuryQueue.Workflow.Action.Mercury.CheckForCompletedOutbound {

    public class CheckForCompletedOutboundCalculatePageCount : MercuryContextStepAction {

        public const string ARG_PAGE_SIZE = "PageSize";
        public const string ARG_PAGE_COUNT = "PageCount";
        public const string ARG_PAGE_START = "PageStart";


        public CheckForCompletedOutboundCalculatePageCount(IUnityContainer container, ProcessInfo processInfo, MercurySettings mercurySettings) : base(container, processInfo, mercurySettings) {

            EnableService<IDocumentReadService>();
            EnableService<IDocumentPersistService>();
            EnableService<ICacheManager>();
            EnableService<IMethaStorage>();
            EnableService<ISystemSettingManager>();

            AddArgument<int>(ARG_PAGE_SIZE, Direction.Out);
            AddArgument<int>(ARG_PAGE_COUNT, Direction.Out);
            AddArgument<int>(ARG_PAGE_START, Direction.Out);

        }

        protected override string GetOperationGroup() {
            return Consts.OP_GROUP_GET_STOCK_ENTRIES;
        }

        protected override bool ActionBody(BaseProcess action) {
            bool isProcessed = true;
            SetValue(ARG_PAGE_COUNT, 0);
            try {
                LocalContext.SaveLog($"{MoscowDateTimeOffset().ToString()} Начало обработки ответов от системы 'Меркурий'.");

                IList<MercuryQueueStepModel> responses = StepService.GetStepsByStatus(QueryId, null, 0, "0.0_001", Consts.OP_GROUP_CHECK_FOR_COMPLETED_OUTBOUND);
                MercuryQueueStepModel completed = responses.Where(r => r.StatusName == MQSStatus.COMPLETE.ToString()).FirstOrDefault();

                IList<Guid> updIds = new List<Guid>();
                // Обработка успешных ответов
                if (completed != null) {
                    MercuryNS.Body body = ParseMercuryHelper.ParseBodyResponseFile(Path.Combine(MercurySettings.ResponseDirectory, completed.FileResponse));
                    if (body.ReceiveApplicationResultResponse != null && body.ReceiveApplicationResultResponse.Application != null) {
                        MercuryNS.GetStockEntryListResponse response = body.ReceiveApplicationResultResponse.Application.Result as MercuryNS.GetStockEntryListResponse;
                        MercuryNS.StockEntryList list = response?.StockEntryList;
                        if (list != null) {
                            int totalItems = list.Total;
                            if (totalItems > 0) {
                                ISystemSettingManager systemSettings = GetService<ISystemSettingManager>();
                                int maxPageCount = systemSettings.GetByPrefix<int>("MIP_OP_GET_STOCK_ENTRY_LIST_PAGE_COUNT", 10);
                                if (maxPageCount < 1) {
                                    maxPageCount = 1;
                                    LocalContext.SaveLog($"Минимальное количество запрашиваемых страниц: 1.");
                                }
                                int pageSize = systemSettings.GetByPrefix<int>("MIP_OP_GET_STOCK_ENTRY_LIST_PAGE_SIZE", 250);
                                if (pageSize < 1) { // проверка отрицательного значения и = 0
                                    pageSize = 1;
                                    LocalContext.SaveLog($"Минимальное количество элементов при зпросе 1.");
                                }
                                int currentPageCount = totalItems / pageSize + ((totalItems % pageSize) > 0 ? 1 : 0);
                                if (currentPageCount > maxPageCount) {
                                    LocalContext.SaveLog($"Количество страниц первышает максимально возможное в запросе. Расчитанне количество страниц: {currentPageCount}");
                                    currentPageCount = maxPageCount;
                                }
                                LocalContext.SaveLog($"Будет запрошено {currentPageCount} страниц с количеством элементов на страницу {pageSize}");
                                SetValue(ARG_PAGE_SIZE, pageSize);
                                SetValue(ARG_PAGE_COUNT, currentPageCount);
                                SetValue(ARG_PAGE_START, 1);
                                ResultDescription = "Количество страниц расчитанно.";
                            } else {
                                LocalContext.SaveLog("Площадка не содержит складских записей.");
                                ResultDescription = "Площадка не содержит складских записей.";
                            }
                        }
                    } else {
                        isProcessed = false;
                        AppendUserErrorInfoFromXML(completed, Guid.Empty, null, null); 
                        LocalContext.SaveLog("Ошибка при ответе системы 'Меркурий'. Смотри файл ответа от системы 'Меркурий'.");
                        ResultDescription = "Система 'Меркурий' вернула исключение";
                    }
                } else { 
                    MercuryQueueStepModel notcompleted = responses.Where(r => r.StatusName != MQSStatus.COMPLETE.ToString()).FirstOrDefault();
                    if (notcompleted != null) { 
                        AppendUserErrorInfoFromXML(notcompleted, Guid.Empty, null, null); 
                    }

                    isProcessed = false;
                    ResultDescription = "Ошибка анализа числа страниц.";
                    LocalContext.SaveLog("Ошибка автоматического вычисления количества страниц для запроса.");
                }
            } catch (Exception e) {
                AppendUserErrorInfo(Guid.Empty, null, UserErrorConsts.ET_MIP, UserErrorConsts.StockList.RTE_CALC_PAGES, UserErrorConsts.DEFAULT_LAS_SUPPRT_ERROR);
                isProcessed = false;
                LocalContext.SaveLog(e.ToString());
                ResultDescription = "Ошибка анализа числа страниц.";

            }
            LocalContext.SaveLog($"{MoscowDateTimeOffset().ToString()} Окончание обработки ответов от системы 'Меркурий'.");
            return isProcessed;
        }

        protected override string GetStepId() {
            return "6.4";
        }

        protected override string GetStepName() {
            return nameof(CheckForCompletedOutboundCalculatePageCount);
        }
    }
}
