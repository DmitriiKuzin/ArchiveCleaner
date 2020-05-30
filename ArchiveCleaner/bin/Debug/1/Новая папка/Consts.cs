// ----------------------------------------------------------------------
// <copyright file="Consts.cs" company="Смарт-Ком">
//     Copyright statement. All right reserved
// </copyright>
// Дата создания: 13.4.2018 
// Проект: Mercury integration platform
// Версия: 3.0 (Refactoring)
// Автор: Василий Ермаков (EMail: vasiliy.ermakov@smart-com.su)
// ------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Module.MercuryQueue.Model {

    public class Consts {

        public const string MARS_ENTITY_CODE = "261";

        public const string MARS_ENTITY_GUID = "fde895e7-218a-11e2-a69b-b499babae7ea";

        /// <summary>
        /// Получение списка ВСД
        /// </summary>
        public const string QUEUE_GET_EVC_DOCUMENTS = "GetEVCDocuments";

        /// <summary>
        /// Получение списка  измененных ВСД
        /// </summary>
        public const string QUEUE_GET_EVC_CHANGES = "GetEVCChanges";

        /// <summary>
        /// Получение списка возвратных ВСД
        /// </summary>
        public const string QUEUE_GET_RETURNABLE_EVC_CHANGES = "GetReturnableEVCChanges";

        /// <summary>
        ///   ВСД по Uuid
        /// </summary>
        public const string QUEUE_GET_EVC_BY_UUID = "GetEVCByUuid";

        /// <summary>
        /// Операция корректировки складских записей
        /// </summary>
        public const string QUEUE_RESOLVE_DISCREPANCY = "ResolveDiscrepancy";

        /// <summary>
        /// Операция копекинг через ResolveDiscrepancy
        /// </summary>
        public const string QUEUE_RESOLVE_DISCREPANCY_COPACKING = "ResolveDiscrepancyCopacking";
        /// <summary>
        /// Операция производственная
        /// </summary>
        public const string QUEUE_PRODUCTION_OPERATION = "ProductionOperation";
        /// <summary>
        /// Финализация производственной операции
        /// </summary>
        public const string QUEUE_FINALIZE_PRODUCTION_OPERATION = "FinalizeProductionOperation";


        public const string QUEUE_MERGE_OPERATION = "MergeOperation";

        /// <summary>
        /// Внесение лабораторных исследований
        /// </summary>
        public const string QUEUE_LABORATORY_RESEARCH = "LaboratoryResearch";

        /// <summary>
        /// Тарнспортная операция
        /// </summary>
        public const string QUEUE_OUTBOUND_OPERATION = "OutboundOperation"; //

        /// <summary>
        /// Тарнспортная операция экспорт
        /// </summary>
        public const string QUEUE_OUTBOUND_EXPORT_OPERATION = "OutboundExportOperation";

        /// <summary>
        /// Операция погашения
        /// </summary>
        public const string QUEUE_INCOMING_OPERATION = "IncomingOperation";

        /// <summary>
        /// Операция погашения
        /// </summary>
        public const string QUEUE_RETURNABLE_OPERATION = "ReturnableOperation";

        /// <summary>
        /// Операция погашения
        /// </summary>
        public const string QUEUE_INCOMING_EVC_OPERATION = "IncomingEVCOperation";

        /// <summary>
        /// Получение складских записей из Меркурия
        /// </summary>
        public const string QUEUE_GET_STOCK_ENTRIES = "GetStockEntries";
        
        /// <summary>
        /// Проверка на предыдущую выписку
        /// </summary>
        public const string QUEUE_CHECK_FOR_COMPLETED_OUTBOUND = "CheckForCompletedOutbound";

        /// <summary>
        /// Анулирование сертификата
        /// </summary>
        public const string QUEUE_WITHDRAW_EVC_DOCUMENTS = "WithdrawEVC";

        /// <summary>
        /// Внесение или изменение информации о продукте в Меркурий
        /// </summary>
        public const string QUEUE_MODIFY_PRODUCER_STOCK_LIST = "ModifyProducerStockList";

        /// <summary>
        /// Проверяет возмодность региональной перевозки
        /// </summary>
        public const string QUEUE_CHECK_SHIPMENT_REGIONALIZATION = "CheckShipmentRegionalization";

        /// <summary>
        /// Операция отправки транспортных данных
        /// </summary>
        public const string QUEUE_UPDATE_TRANSPORT_MOVEMENT_OPERATION = "UpdateTransportMovementOperation";

        /// <summary>
        /// Операция анулирования входящихз ВСД
        /// </summary>
        public const string QUEUE_WITHDRAW_INBOUND_EVC_DOCUMENTS = "WithdrawInboundEVC";


        public const string OP_GROUP_MERGE = "MergeOperation";
        public const string OP_GROUP_PREPARE_OUTGOING_CONSIGNMENT = "OutgoingConsignment";
        public const string OP_GROUP_PREPARE_OUTGOING_EXPORT_CONSIGNMENT = "OutgoingExportConsignment";
        public const string OP_GROUP_RESOLVE_DISCREPANCY = "ResolveDiscrepancy";
        public const string OP_GROUP_RESOLVE_DISCREPANCY_COPACKING = "ResolveDiscrepancyCopacking";
        public const string OP_GROUP_PRODUCTION_OPERATION = "ProductionOperation";
        public const string OP_GROUP_FINALIZE_PRODUCTION_OPERATION = "FinalizeProductionOperation";
        public const string OP_GROUP_WITHDRAW_VET_DOCUMENT = "WithdrawVetDocument";
        public const string OP_GROUP_INCOMING_OPERATION = "IncomingOperation"; 
        public const string OP_GROUP_RETURNABLE_OPERATION = "ReturnableOperation";
        public const string OP_GROUP_LABORATORY_RESEARCH = "LaboratoryResearch";
        public const string OP_GROUP_CHECK_FOR_COMPLETED_OUTBOUND = "CheckForCompletedOutbound";
        public const string OP_GROUP_GET_STOCK_ENTRIES = "GetStockEntries";
        public const string OP_GROUP_GET_EVC_DOCUMENTS = "GetEVCDocuments";
        public const string OP_GROUP_MODIFY_PRODUCER_STOCK_LIST = "ModifyProducerStockList";
        public const string OP_GROUP_CHECK_SHIPMENT_REGIONALIZATION = "CheckShipmentRegionalization";
        public const string OP_GROUP_UPDATE_TRANSPORT_MOVEMENT = "UpdateTransportMovement";

        public const string STEP_NAME_MERCURY_COMMUNICATE = "MercuryCommunicate";


        public const string MERCURY_PREPARE_OUTGOING_CONSIGNMENT_OPERATION = "PrepareOutgoingConsignmentOperation";
        public const string MERCURY_PREPARE_OUTGOING_EXPORT_CONSIGNMENT_OPERATION = "PrepareOutgoingExportConsignmentOperation";
        public const string MERCURY_GET_STOCK_ENTRY_LIST_OPERATION = "GetStockEntryListOperation";
        public const string MERCURY_CHECK_FOR_COMPLETED_OUTBOUND_OPERATION = "CheckForCompletedOutboundOperation";
        public const string MERCURY_RESOLVE_DISCREPANCY_OPERATION = "ResolveDiscrepancyOperation";
        public const string MERCURY_RESOLVE_DISCREPANCY_COPACKING_OPERATION = "ResolveDiscrepancyCopackingOperation";
        public const string MERCURY_PRODUCTION_OPERATION = "ProductionOperation";
        public const string MERCURY_GET_VET_DOCUMENT_LIST_OPERATION = "GetVetDocumentListOperation";
        public const string MERCURY_GET_VET_DOCUMENT_BY_UUID_OPERATION = "GetVetDocumentByUuidOperation";
        public const string MERCURY_GET_VET_DOCUMENT_CHANGES_LIST_OPERATION = "GetVetDocumentChangesListOperation";
        public const string MERCURY_MERGE_OPERATION = "MergeOperation";
        public const string MERCURY_WITHDRAW_VET_DOCUMENT_OPERATION = "WithdrawVetDocumentOperation";
        public const string MERCURY_UPDATE_VETERINARY_EVENTS_OPERATION = "UpdateVeterinaryEventsOperation";
        public const string MERCURY_INCOMING_OPERATION = "IncomingOperation";
        public const string MERCURY_RETURNABLE_OPERATION = "ReturnableOperation";
        public const string MERCURY_INCOMING_EVC_OPERATION = "IncomingEVCOperation";
        public const string MERCURY_MODIFY_PRODUCER_STOCK_LIST_OPERATION = "ModifyProducerStockListOperation";
        public const string MERCURY_CHECK_SHIPMENT_REGIONALIZATION_OPERATION = "CheckShipmentRegionalizationOperation";
        public const string MERCURY_UPDATE_TRANSPORT_MOVEMENT_OPERATION = "UpdateTransportMovementOperation";



        public const string MERCURY_VET_DOCUMENT_STATUS_CONFIRMED = "CONFIRMED";
        public const string MERCURY_VET_DOCUMENT_STATUS_WITHDRAWN = "WITHDRAWN";
        public const string MERCURY_VET_DOCUMENT_STATUS_UTILIZED = "UTILIZED";


        //public const string UOM_MEINS_PC = "PC";
        //public const string UOM_MEINS_EA = "EA";
        //public const string UOM_MEINS_CS = "CS";

        public class MATERIAL_TYPE_SAP {
            public const string FERT = "FERT";
            public const string ROH = "ROH";

        }
        public class PACKAGE_LEVEL {
            /// <summary>
            /// Транспортный (Логистический) уровень.
            /// Товар в упаковке, предназначенной для отгрузки покупателю (ритейлеру) при выполнении заказа.
            /// </summary>
            public const int TRANSPORT = 6;

            /// <summary>
            /// Дополнительный уровень. 
            /// Товар в упаковке, которую нельзя однозначно отнести к торговому или транспортному уровню.
            /// </summary>
            public const int TRANSPORT_TRADE = 5;

            /// <summary>
            /// Торговый уровень. 
            /// Товар в упаковке, предназначенной для заказа, оплаты и доставки. 
            /// Это согласованный между ритейлером и изготовителем (или другим участником) уровень упаковки товара, 
            /// в котором товар заказывается, оплачивается и доставляется.
            /// </summary>
            public const int TRADE = 4;

            /// <summary>
            /// Промежуточный уровень. 
            /// Уровень упаковки, если он существует, который находится между потребительским и торговым уровнем.
            /// </summary>
            public const int TRADE_CONSUME = 3;

            /// <summary>
            /// Потребительский уровень. 
            /// Товар в упаковке для розничной торговли, маркированный штриховым кодом для сканирования на кассе.
            /// </summary>
            public const int CONSUME = 2;

            /// <summary>
            /// Внутренний уровень. Уровень, при котором упаковка отсутствует, но тем не менее есть необходимость наносить маркировку. 
            /// Например, яйцо, шкуры, мясо, сыр. Явно указывается, что упаковка отсутствует.
            /// </summary>
            public const int WITHOUT_PACK = 1;

        }

        public class UNIT_OF_MEASURE {

            /// <summary>
            /// Коробка.
            /// </summary>
            public const string CS = "CS";

            /// <summary>
            /// Коробка.
            /// </summary>
            public const string EA = "EA";

            /// <summary>
            /// Поддон деревянный.
            /// </summary>
            public const string PC = "PC";

            /// <summary>
            /// 
            /// </summary>
            public const string PAL = "PAL";
        }

        public class PACKING_CODE {

            /// <summary>
            /// Штука.
            /// </summary>
            public const string PP = "PP";

            /// <summary>
            /// Бандероль.
            /// </summary>
            public const string PC = "PC";

            /// <summary>
            /// Коробка.
            /// </summary>
            public const string BOX = "BX";

            /// <summary>
            /// Поддон деревянный.
            /// </summary>
            public const string PALLETE = "PX";

            /// <summary>
            /// Коробка из фибрового картона.
            /// </summary>
            public const string FIBREBOX = "4G";
        }

        public class PRODUCT_MARKING {

            /// <summary>
            /// Номер производственной партии.
            /// Важно, чтобы номера партий совпадали при совершении операции незавершённого производства.
            /// </summary>
            public const string BN = "BN";

            /// <summary>
            /// SSCC-код(глобально-уникальный код грузовых контейнеров - Serial Shipping Container Code).
            /// </summary>
            public const string SSCC = "SSCC";

            /// <summary>
            /// Маркировка вышестоящей групповой упаковки, например, паллеты. 
            /// Может использоваться для поиска группы вет.сертификатов для партий, находящихся на данной паллете.
            /// </summary>
            public const string BUNDLE = "BUNDLE";
        }

        /// <summary>
        /// Типы пользователей Меркурия
        /// </summary>
        public class MERCURY_USER_DETERMINATION {

            /// <summary>
            /// Пользователь по умолчанию
            /// </summary>
            public const string DEFAULT_USER = "DEFAULT_USER";

            /// <summary>
            /// Пользователь полученные на основании расписания смен
            /// </summary>
            public const string SCHEDULE = "SCHEDULE";
        }

        /// <summary>
        /// Типы ролей пользователей Меркурия
        /// </summary>
        public class MERCURY_USER_ROLE {

            /// <summary>
            /// Роль транспортного оператора
            /// </summary>
            public const string TRANSPORT = "TRANSP";

            /// <summary>
            /// Роль ветеринарного врача
            /// </summary>
            public const string VETERINARY = "VET";

            /// <summary>
            /// Роль производственой операции
            /// </summary>
            public const string PRODUCTIVE = "PROD";
        }

        public class CACHEKEYS {
            public const string MERCURY_QUEUE_STOP_OPERATIONS = "StopQueueOperations";
            public const string MERCURY_QUEUE_STOP_STEPS = "StopQueueSteps";
            public const string MERCURY_QUEUE_NEED_UNBLOCK_ENTERPRISE = "NeedUnblockEnterprise";
            public const string DB_AVAILABILITY = "dbAvalability";
            public const string DB_AVAILABILITY_PREVIOUS = "dbAvalabilityPrevious";
            

        }


        public const string UNKNOWN_MERCURY_LOGIN = "Не удалось получить логин для системы 'Меркурий'. \n" +
                            "Возможные причины: \n" +
                            "  1. Нет привязки пользователя к логину мркурия.\n" +
                            "  2. Нет привязки пользователя к логину на основании текущей смены.\n" +
                            "  3. Нет информации о пользователе по умолчанию.\n";

        public const string SEE_ADDITIONAL_ERROR_INFO = "Дополнительную информацию смотри по кнопке 'Ошибки'";


        internal const string DEFAULT_ERROR_CODE_FIELD = "ErrCode";
        internal const string DEFAULT_ERROR_MESSAGE_FIELD = "ErrMessage";
        internal const string PROCESSED_MARK = "PROCESSED";
        internal const string STOP_QUEUE_STEPS_ARG = "StopQueueSteps";


        
    }
}
