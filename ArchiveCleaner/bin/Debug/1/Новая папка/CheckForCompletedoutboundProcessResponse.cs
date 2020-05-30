// ----------------------------------------------------------------------
// <copyright file="StockListMakeRequest.cs" company="Смарт-Ком">
//     Copyright statement. All right reserved
// </copyright>
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
using Module.MercuryQueue.Workflow.Action.Mercury.PrepareOutgoingConsignment;

namespace Module.MercuryQueue.Workflow.Action.Mercury.CheckForCompletedOutbound
{
    public class CheckForCompletedoutboundProcessResponse : MercuryContextStepAction
    {
        public const string ARG_PAGE_ENTERPRISE_ID = "EnterpriseId";
        public const string ARG_UPDATED_STOCK_ENTRY_IDS = "UpdatedStockEntryIds";
        public const string ARG_REMOVE_IS_ABSENT = "RemoveIfIsAbsentInMercury";
        public const string ARG_SKIPEED_STOCK_ENTRY_IDS = "SkippedStockEntryIds";
        public const string ARG_STOCK_ENTRY_IDS = "StockEntryIds";


        public CheckForCompletedoutboundProcessResponse(IUnityContainer container, ProcessInfo processInfo,
            MercurySettings mercurySettings, IQueryGenerator generator) : base(container, processInfo, mercurySettings)
        {
            EnableService<IDocumentReadService>();
            EnableService<IDocumentPersistService>();
            EnableService<ICacheManager>();
            EnableService<IMethaStorage>();
            EnableService<IMercuryQueueService>();


            AddArgument<Guid>(ARG_PAGE_ENTERPRISE_ID);
            AddArgument<IEnumerable<Guid>>(ARG_STOCK_ENTRY_IDS, Direction.In);
            AddArgument<bool>(ARG_REMOVE_IS_ABSENT, Direction.In, true);
            AddArgument<IList<Guid>>(ARG_UPDATED_STOCK_ENTRY_IDS, Direction.InOut, new List<Guid>());
            AddArgument<IList<Guid>>(ARG_SKIPEED_STOCK_ENTRY_IDS, Direction.InOut, new List<Guid>());
        }

        protected override bool ActionBody(BaseProcess action)
        {
            bool isProcessed = true;
            IAction updateAction = null;
            
            
            try
            {
                Guid enterpriseId = GetValue<Guid>(ARG_PAGE_ENTERPRISE_ID);
                bool removeIsAbsent = GetValue<bool>(ARG_REMOVE_IS_ABSENT);
                var stockEntriesOutbound = GetValue<IEnumerable<Guid>>(ARG_STOCK_ENTRY_IDS);

                LocalContext.SaveLog(
                    $"{MoscowDateTimeOffset().ToString()} Начало обработки ответов от системы Меркурий.");

                // Получение последнего номера шага
                IList<MercuryQueueStepModel> responses = StepService.GetStepsByStatus(QueryId, null, 0, "0.0", null);

                IList<MercuryQueueStepModel> completed = responses.Where(r =>
                    r.StatusName == MQSStatus.COMPLETE.ToString() && r.ProcessMark != Consts.PROCESSED_MARK).ToList();

                bool anyError = responses.Any(x => x.StatusName != MQSStatus.COMPLETE.ToString());
                List<Guid> updIds = GetValue<List<Guid>>(ARG_UPDATED_STOCK_ENTRY_IDS);
                List<Guid> skippedIds = GetValue<List<Guid>>(ARG_SKIPEED_STOCK_ENTRY_IDS);

                // Обработка успешных ответов
                if (completed.Count > 0)
                {
                    foreach (MercuryQueueStepModel step in completed)
                    {
                        if (NeedStopSteps())
                        {
                            LocalContext.SaveLog(
                                $" Выполннение прервано (StopQueueSteps). Выполнение будет продолжено после запуска очереди.");
                            SetValue("Status", MQSStatus.STOPPED);
                            return true;
                        }

                        MercuryNS.Body body =
                            ParseMercuryHelper.ParseBodyResponseFile(Path.Combine(MercurySettings.ResponseDirectory,
                                step.FileResponse));
                        if (body.ReceiveApplicationResultResponse != null &&
                            body.ReceiveApplicationResultResponse.Application != null)
                        {
                            MercuryNS.GetStockEntryListResponse response =
                                body.ReceiveApplicationResultResponse.Application.Result as
                                    MercuryNS.GetStockEntryListResponse;
                            MercuryNS.StockEntryList stockEntriesFromMerc = response?.StockEntryList;
                            if (stockEntriesFromMerc != null && stockEntriesFromMerc.Count > 0)
                            {
                                LocalContext.SaveLog(
                                    $" Обработка файла ответа от Меркурия. Имя файла {step.FileResponse}.");

                                var queryForPullStockEntryGuids = new DocumentQuery(GetService<IMethaStorage>(),GetService<IQueryGenerator>(), GetService<ICacheManager>());
                                queryForPullStockEntryGuids
                                    .ResultField("SE", "@Id")
                                    .ResultField("SE", "GUID")
                                    .Using("StockEntry", "SE", ObjectStatus.osActive);
                                queryForPullStockEntryGuids.Where(w => w.Or(o =>
                                {
                                    foreach (var seId in stockEntriesOutbound)
                                    {
                                        o.EQ("@Id", seId);
                                    }
                                }));
                                
                                var seOutboundIds = LocalReader.QueryDocuments(queryForPullStockEntryGuids).Result.Select(d => d["GUID"]);
                                
                                var stockEntriesFromMip = LoadStockEntriesFromMIP(stockEntriesFromMerc);

                                foreach (var se in stockEntriesFromMerc.Where(d=>seOutboundIds.Contains(d.GUID)))
                                {
                                    var seFromMIP = stockEntriesFromMip.FirstOrDefault(d => (string) d["GUID"] == (string) se.GUID);

                                    if ((double) se.Batch.Volume < (double) seFromMIP["Volume"])
                                    {
                                        isProcessed = false;
                                        LocalContext.SaveLog(
                                            $" Выписка уже производилась");
                                    }

                                    if ((string) se.UUID != (string) seFromMIP["UUID"])
                                    {
                                        isProcessed = false;
                                        LocalContext.SaveLog(
                                            $" Выписка уже производилась");
                                    }
                                }
                            }
                            else
                            {
                                LocalContext.SaveLog(
                                    $" Файл ответа {step.FileResponse} от системы 'Меркурий' не содержит информацию о складских записях.");
                            }
                        }
                        else
                        {
                            anyError = true;
                            body.Fault.ToString();
                            // TODO сохранить ошибку в логе
                            //body.Fault
                            ResultDescription = "Система 'Меркурий' вернула исключение";
                        }
                    }
                }
                else
                {
                    isProcessed = false;
                    ResultDescription = "Нет успешно обработанных ответов";
                    LocalContext.SaveLog(ResultDescription);
                }

                var notCompleted = responses.Where(r => r.StatusName != MQSStatus.COMPLETE.ToString());
                foreach (var nc in notCompleted)
                {
                    AppendUserErrorInfoFromXML(nc, Guid.Empty, null, null);
                }

                if (string.IsNullOrWhiteSpace(ResultDescription))
                    ResultDescription = "Обработаны ответы от системы 'Меркурий'.";
            }
            catch (ConcurrencyDocException except)
            {
                isProcessed = false;
                DefaultConcurrentExceptionLog(except);
            }
            catch (Exception e)
            {
                AppendUserErrorInfo(Guid.Empty, null, UserErrorConsts.ET_MIP,
                    UserErrorConsts.StockList.RTE_PROCESS_RESPONSE, UserErrorConsts.DEFAULT_LAS_SUPPRT_ERROR);
                isProcessed = false;
                LocalContext.SaveLog(e.ToString());
                ResultDescription = "Ошибка анализа результатов";
            }

            LocalContext.SaveLog(
                $"{MoscowDateTimeOffset().ToString()} Окончание обработки ответов от системы 'Меркурий'.");
            return isProcessed;
        }

        protected override string GetStepId()
        {
            return "6.2";
        }

        protected override string GetStepName()
        {
            return "CheckForCompletedOutbound";
        }
        
        protected override string GetOperationGroup() {
            return Consts.OP_GROUP_CHECK_FOR_COMPLETED_OUTBOUND;
        }

        private IEnumerable<Document> LoadStockEntriesFromMIP(IEnumerable<MercuryNS.StockEntry> source)
        {
           
            List<Document> stockEntries = new List<Document>();

            //вынести перед циклом
            DocumentQuery query = new DocumentQuery(GetService<IMethaStorage>(), GetService<IQueryGenerator>(), GetService<ICacheManager>())
                .ResultField("SE", "*")
                .Using("StockEntry", "SE", ObjectStatus.osActive);

            foreach (IEnumerable<string> guids in source.Select(s => s.GUID).Batch(100))
            {
                stockEntries.AddRange(LocalReader.QueryDocuments(query.Where(w => w.Or(a =>
                {
                    foreach (string g in guids)
                        a.EQ("GUID", g, "SE");
                }))).Result);
            }
            return stockEntries;
        }
    }
}