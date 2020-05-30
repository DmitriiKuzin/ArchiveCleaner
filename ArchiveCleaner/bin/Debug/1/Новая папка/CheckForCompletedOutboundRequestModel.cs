// ----------------------------------------------------------------------
// <copyright file="GetStockEntryListRequestModel.cs" company="Смарт-Ком">
//     Copyright statement. All right reserved
// </copyright>
// Дата создания: 9.11.2017 
// Проект: Mercury integration platform
// Версия: 1.0
// Автор: Василий Ермаков (EMail: vasiliy.ermakov@smart-com.su)
// ------------------------------------------------------------------------

using System;

namespace Module.MercuryQueue.Model.Requests {

    public class CheckForCompletedOutboundRequestModel : BaseRequestModel {

        public string LocalTransactionId { get; set; }

        public string Login { get; set; }

        public int Count { get; set; }

        public int Offset { get; set; }

        public string EnterpriseGuid { get; set; }

        public string EnterpriseCode { get; set; }

        public DateTimeOffset? ReceiptBeginDate { get; set; }

        public DateTimeOffset? ReceiptEndDate { get; set; }

    }
}
