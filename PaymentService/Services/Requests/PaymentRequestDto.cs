namespace PaymentService.Services.Requests
{
    public class PaymentRequestDto
    {
        public decimal amount { get; set; }
        public string currency { get; set; } = string.Empty;
        public List<int> payment_methods { get; set; } = new();
        public List<PaymentItem> items { get; set; } = new();
        public string redirection_url { get; set; } = string.Empty;
        public BillingData billing_data { get; set; } = new();
        public PaymentExtras? extras { get; set; }
    }

    public class PaymentItem
    {
        public string name { get; set; } = string.Empty;
        public int quantity { get; set; }
        public decimal amount { get; set; }
    }

    public class BillingData
    {
        public string first_name { get; set; } = "NA";
        public string last_name { get; set; } = "NA";
        public string email { get; set; } = "NA";
        public string phone_number { get; set; } = "NA";
        public string apartment { get; set; } = "NA";
        public string floor { get; set; } = "NA";
        public string street { get; set; } = "NA";
        public string building { get; set; } = "NA";
        public string shipping_method { get; set; } = "NA";
        public string postal_code { get; set; } = "NA";
        public string city { get; set; } = "NA";
        public string country { get; set; } = "NA";
        public string state { get; set; } = "NA";
    }

    public class PaymentExtras
    {
        public string booking_id { get; set; } = string.Empty;
    }
}
