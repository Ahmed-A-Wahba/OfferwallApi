namespace OfferwallApi.Shared.Entities;

public sealed class Partner
{
    public Guid PartnerId { get; set; }

    // Contact Person
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }

    // Company
    public string CompanyName { get; set; } = null!;
    public string? LegalName { get; set; }
    public string? TradingName { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? RegisteredAddress { get; set; }
    public string? TaxRegistrationNumber { get; set; }
    public Country TaxResidencyCountry { get; set; } = Country.Other;


    public Country CountryCode { get; set; } = Country.Other;
    public Country? TaxResidencyCountryCode { get; set; }

    // Branding
    public string? LogoUrl { get; set; }
    public string? WebsiteUrl { get; set; }

    public StoreCategory StoreCategory { get; set; } = StoreCategory.Other;
    public int MonthlyActiveUsers { get; set; }

    // Authentication
    public string PasswordHash { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public string ApiSecret { get; set; } = null!;
    public string? AllowedIframeOrigin { get; set; }

    // Offerwall
    public int PointsPerUsd { get; set; }

    // Payment
    public string? AccountHolderName { get; set; }
    public string? AccountName { get; set; }
    public string? BankName { get; set; }
    public string? Iban { get; set; }
    public string? SwiftCode { get; set; }
    public string? Currency { get; set; }
    public PayoutMethod PayoutMethod { get; set; }
    public string? TransferType { get; set; }
    public string? RoutingNumber { get; set; }
    public string? AccountNumber { get; set; }
    public AccountType AccountType { get; set; }
    public string? AccountHolderAddress { get; set; }

    // Contact Person
    public string? ContactFullName { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhoneNumber { get; set; }

    public bool HasChargebacks { get; set; }
    public PublisherStatus Status { get; set; } = PublisherStatus.PendingApproval;
    public bool IsEmailVerified { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public decimal RevenueShareRatio { get; set; } = 0.8m;

    public ICollection<User> Users { get; set; } = [];
    public ICollection<Payout> Payouts { get; set; } = [];
    public ICollection<Coupon> Coupons { get; set; } = [];
    public ICollection<PartnerRefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<PartnerVerificationCode> VerificationCodes { get; set; } = [];
    public ICollection<Conversion> Conversions { get; set; } = [];
}

public enum StoreCategory
{
    Gaming,
    ECommerce,
    Education,
    Health,
    Finance,
    Travel,
    Entertainment,
    Other
}

public enum PublisherStatus
{
    PendingApproval,
    Active,
    Disabled
}

public enum Country
{
    US,
    CA,
    GB,
    AU,
    DE,
    FR,
    IT,
    ES,
    NL,
    SE,
    NO,
    DK,
    FI,
    BE,
    CH,
    AT,
    IE,
    PT,
    GR,
    PL,
    CZ,
    HU,
    SK,
    SI,
    HR,
    RO,
    BG,
    TR,
    RU,
    CN,
    JP,
    KR,
    IN,
    BR,
    MX,
    AR,
    CL,
    CO,
    PE,
    VE,
    ZA,
    NG,
    EG,
    KE,
    TZ,
    UG,
    GH,
    SN,
    CI,
    MA,
    DZ,
    TN,
    LY,
    SD,
    ET,
    ZW,
    MZ,
    ZM,
    MW,
    BW,
    NA,
    LS,
    SZ,
    Other
}

public enum PayoutMethod
{
    BankTransfer = 1
}

public enum AccountType
{
    Personal = 1,
    Business = 2
}