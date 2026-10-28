namespace PaymentService.Services.Responses
{
    public class PaymobIntentionResponse
    {
        public string? id { get; set; }
        public string? client_secret { get; set; }
        public long? intention_order_id { get; set; }
        public string? status { get; set; }
    }

    public class PaymobWebhookPayload
    {
        public string? type { get; set; }
        public PaymobTransactionObj? obj { get; set; }
    }

    public class PaymobTransactionObj
    {
        public long id { get; set; }
        public bool success { get; set; }
        public bool pending { get; set; }
        public bool error_occured { get; set; }
        public bool is_voided { get; set; }
        public bool is_refunded { get; set; }
        public long amount_cents { get; set; }
        public string? currency { get; set; }
        public string? created_at { get; set; }
        public bool has_parent_transaction { get; set; }
        public bool is_3d_secure { get; set; }
        public bool is_auth { get; set; }
        public bool is_capture { get; set; }
        public bool is_standalone_payment { get; set; }
        public long integration_id { get; set; }
        public long owner { get; set; }
        public PaymobOrder? order { get; set; }
        public PaymobSourceData? source_data { get; set; }
    }

    public class PaymobOrder
    {
        public long id { get; set; }
        // merchant_order_id is the extras.booking_id we passed during intention creation
        public string? merchant_order_id { get; set; }
        public long amount_cents { get; set; }
        public string? currency { get; set; }
    }

    public class PaymobSourceData
    {
        public string? type { get; set; }
        public string? sub_type { get; set; }
        public string? pan { get; set; }
    }
}
