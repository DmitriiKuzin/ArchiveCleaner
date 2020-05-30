// ----------------------------------------------------------------------
// <copyright file="ModuleRegistrator.cs" company="Смарт-Ком">
//     Copyright statement. All right reserved
// </copyright>
// Дата создания: 2.4.2018 
// Проект: Mercury integration platform
// Версия: 3.0 (Refactoring)
// Автор: Василий Ермаков (EMail: vasiliy.ermakov@smart-com.su)
// ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Reflection;
using Module.Core;
using System.Linq;
using Engine.Core.Service;
using Engine.Workflow;
using Module.MercuryQueue.Model;
using Module.MercuryQueue.Service;
using Module.MercuryQueue.Workflow;
using Module.MercuryQueue.Workflow.Process;
using Module.MercuryQueue.Workflow.Process.Mercury;

namespace Module.MercuryQueue {

    [Export(typeof(IModuleRegistrator))]
    public class ModuleRegistrator : IModuleRegistrator {

        public String ModuleName() {
            return "MercuryQueue";
        }

        public IEnumerable<ExportServiceInfo> GetAvailableServices() {
            IList<ExportServiceInfo> result = new List<ExportServiceInfo>();

            //Все процессы
            Type t = typeof(IEventProcessor);
            foreach (var item in Assembly.GetExecutingAssembly().GetTypes().Where(x => t.IsAssignableFrom(x) && x.IsClass)) {
                result.Add(new ExportServiceInfo() { InterfaceType = t, ImplementType = item });
            }

            //Все действия
            t = typeof(IAction);
            foreach (var item in Assembly.GetExecutingAssembly().GetTypes().Where(x => t.IsAssignableFrom(x) && x.IsClass)) {
                result.Add(new ExportServiceInfo() { InterfaceType = t, ImplementType = item });
            }

            //Все команды
            //t = typeof(ISQLCommand);
            //foreach (var item in Assembly.GetExecutingAssembly().GetTypes().Where(x => t.IsAssignableFrom(x) && x.IsClass)) {
            //    result.Add(new ExportServiceInfo() { InterfaceType = t, ImplementType = item });
            //}
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(InitMercuryCommunicateDelayProcess) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(InitMercuryConnectSettingsProcess) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(InitMercurySettingsProcess) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetEVCDocuments) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetEVCDocuments_V2_0) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetEVCDocuments_V2_1) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetEVCByUuid) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetEVCChanges) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetReturnableEVCChanges) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetStockEntries) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetStockEntries_V2_0) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryGetStockEntries_V2_1) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryMergeOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryResolveDiscrepancy) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryResolveDiscrepancyCopacking) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryOutboundOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryOutboundExportOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryIncomingOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryReturnableOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryIncomingEVCOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryProductionOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryFinalizeProductionOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryWithdrawEVC) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryWithdrawInboundEVC) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryLaborResearch) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryLaborResearch_V2_0) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryLaborResearch_V2_1) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryModifyProducerStockList) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryCheckShipmentRegionalization) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(ResetGenerateLockerProcess) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryUpdateTransportMovementOperation) });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IProcess), ImplementType = typeof(MercuryCheckForCompletedOutbound) });

            //result.Add(new ExportServiceInfo() { InterfaceType = typeof(IDatabaseDataService), ImplementType = typeof(DatabaseDataService), Default = true });
            MercurySettings sett = new MercurySettings();
            result.Add(new ExportServiceInfo() { InterfaceType = null, ImplementType = typeof(MercurySettings), Default = true, IsSingleton = true, Object = sett });
            result.Add(new ExportServiceInfo() { InterfaceType = null, ImplementType = typeof(MercuryConnectSettings), Default = true, IsSingleton = true, Object = new MercuryConnectSettings() });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IMercuryUserPersistService), ImplementType = typeof(MercuryUserPersistService), Default = true });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IMercuryMessageBodyGenerator), ImplementType = typeof(MercuryMessageBodyGenerator), Default = true, IsSingleton = true });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IMercuryQueueStepService), ImplementType = typeof(MercuryQueueStepService), Default = true });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IMercuryQueueService), ImplementType = typeof(MercuryQueueService), Default = true });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IMercuryCommunicateDelaySettings), ImplementType = typeof(MercuryCommunicateDelaySettings), Default = true, IsSingleton = true });
            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IGenerateLocker), ImplementType = typeof(GenerateLocker), Default = true, IsSingleton = true });

            result.Add(new ExportServiceInfo() { InterfaceType = typeof(IGenerateProcessLogWriter), ImplementType = typeof(GenerateProcessLogWriter), Default = true});

            return result;
        }

        public IEnumerable<SettingInfo> GetSystemSettings() {
            return new SettingInfo[] { };
        }

        public IEnumerable<string> GetRoles() {
            return new String[] {
                "ADMINISTRATOR"
            };
        }

        public void InitModuleCallback<T>(T registerType) {
            if (typeof(T).Equals(typeof(IProcessorManager))) ModuleMercuryQueueProcessorManager.Init((IProcessorManager) registerType);
        }
    }
}
