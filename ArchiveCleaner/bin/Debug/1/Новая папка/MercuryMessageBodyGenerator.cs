// ----------------------------------------------------------------------
// <copyright file="IMercuryMessageBodyGenerator.cs" company="Смарт-Ком">
//     Copyright statement. All right reserved
// </copyright>
// Дата создания: 4.4.2018 
// Проект: Mercury integration platform
// Версия: 3.0 (Refactoring)
// Автор: Василий Ермаков (EMail: vasiliy.ermakov@smart-com.su)
// ------------------------------------------------------------------------

using Module.MercuryQueue.Model;
using Module.MercuryQueue.Model.Requests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RazorEngine.Templating;
using Module.MercuryQueue.Model.IncomingOperation;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace Module.MercuryQueue.Service {

    public class MercuryMessageBodyGenerator : IMercuryMessageBodyGenerator {

        private Dictionary<string, Type> operationModel = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase) {
            { Consts.MERCURY_RESOLVE_DISCREPANCY_OPERATION, typeof(ResolveDiscrepancyRequestModel)},
            { Consts.MERCURY_RESOLVE_DISCREPANCY_COPACKING_OPERATION, typeof(ResolveDiscrepancyRequestModel)},
            { "ReceiveApplicationResult",typeof(ReceiveApplicationResultRequestModel)},
            { Consts.MERCURY_GET_VET_DOCUMENT_LIST_OPERATION,typeof(GetVetDocumentListRequestModel)},
            { Consts.MERCURY_MERGE_OPERATION,typeof(MergeOperationRequestModel)},
            { Consts.MERCURY_GET_STOCK_ENTRY_LIST_OPERATION,typeof(GetStockEntryListRequestModel)},
            { Consts.MERCURY_CHECK_FOR_COMPLETED_OUTBOUND_OPERATION,typeof(CheckForCompletedOutboundRequestModel)},
            { Consts.MERCURY_PREPARE_OUTGOING_CONSIGNMENT_OPERATION,typeof(PrepareOutgoingConsignmentOperationRequestModel)},
            { Consts.MERCURY_INCOMING_OPERATION,typeof(IncomingOperationRequestModel)},
            { Consts.MERCURY_RETURNABLE_OPERATION,typeof(IncomingOperationRequestModel)},
            { Consts.MERCURY_INCOMING_EVC_OPERATION,typeof(IncomingOperationRequestModel)},
            { Consts.MERCURY_WITHDRAW_VET_DOCUMENT_OPERATION,typeof(WithdrawVetDocumentRequestModel)} ,
            { Consts.MERCURY_GET_VET_DOCUMENT_BY_UUID_OPERATION,typeof(GetVetDocumentByUuidRequestModel)} ,
            { Consts.MERCURY_GET_VET_DOCUMENT_CHANGES_LIST_OPERATION,typeof(GetVetDocumentChangesListRequestModel)},
            { Consts.MERCURY_UPDATE_VETERINARY_EVENTS_OPERATION,typeof(UpdateVeterinaryEventsRequestModel)},
            { Consts.MERCURY_PRODUCTION_OPERATION,typeof(ProductionOperationModel)},
            { Consts.MERCURY_MODIFY_PRODUCER_STOCK_LIST_OPERATION,typeof(ModifyProducerStockListRequestModel)},
            { Consts.MERCURY_CHECK_SHIPMENT_REGIONALIZATION_OPERATION,typeof(CheckShipmentRegionalizationRequestModel)},
            { Consts.MERCURY_UPDATE_TRANSPORT_MOVEMENT_OPERATION, typeof(UpdateTrasportMovementOperationRequestModel)}
        };

        public MercuryMessageBodyGenerator() { }

        /// <summary>
        /// Генерирует тело сообщения в меркурий
        /// </summary>
        /// <param name="operationName">Наименование операции</param>
        /// <param name="mercurySettings">Настройки для коииуникации с Меркурием</param>
        /// <param name="model">Модель для формирования запроса</param>
        /// <returns>Сгенерированно тело сообщения</returns>
        public string GetMessageBody(string operationName, MercurySettings mercurySettings, object model) {
            // Ключ TemplateKey =  <API VERSION>\<OPERATION NAME>
            // Путь <TEMPLATE DIRECTORY>\TemplateKey
            string templateVersionKey = Path.Combine(mercurySettings.MecuryApiVersion, operationName);
            Type modelType = operationModel[operationName];
            if (!RazorEngine.Engine.Razor.IsTemplateCached(templateVersionKey, modelType)) {
                // Прочитать шаблон с диска
                string path = Path.Combine(mercurySettings.TemplateDirectory, String.Format("{0}.xml", templateVersionKey));
                string templateContent = File.ReadAllText(path, Encoding.UTF8);
                // Закэшировать и скомпилировать
                RazorEngine.Engine.Razor.Compile(templateContent, templateVersionKey, modelType);
            }
            string result = RazorEngine.Engine.Razor.Run(templateVersionKey, modelType, model);
            return result;
        }

        public string GetMessageBodyNoNullTags(string operationName, MercurySettings mercurySettings, object model) {
            string res = GetMessageBody(operationName, mercurySettings, model);
            XDocument xml = XDocument.Parse(res);

            var body = xml.Descendants().FirstOrDefault(a => a.Name.LocalName == "Body");
            if(body != null) {
                var emptyTags = body.Descendants().Where(a => ShouldDelete(a)).ToList();
                foreach(var t in emptyTags)
                    t.Remove();
            }
            return xml.ToString();
        }

        public Boolean ShouldDelete(XElement e) {
            if(!String.IsNullOrWhiteSpace(e.Value)) return false;
            foreach(var child in e.Elements())
                if(child.Descendants().Any(a => !ShouldDelete(a))) return false;
            return true;
        }

        /// <summary>
        /// Перекомпилирует шаблоны создавая новый кеш.
        /// Использовать осторожно. Имеет утечку памяти.
        /// </summary>
        public void ResetTemplates() {

        }
    }
}
