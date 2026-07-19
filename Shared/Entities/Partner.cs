using OfferwallApi.Entities;

namespace OfferwallApi.Shared.Entities;

public sealed class Partner
{
    public Guid PartnerId { get; set; }

    public string FullName { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public string? WebsiteUrl { get; set; }

    public StoreCategory StoreCategory { get; set; } = StoreCategory.Other;

    public int MonthlyActiveUsers { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string ApiKey { get; set; } = null!;

    public string? AllowedIframeOrigin { get; set; }

    public int PointsPerUsd { get; set; }

    public string? AccountHolderName { get; set; }

    public string? BankName { get; set; }

    public string? Iban { get; set; }

    public string? SwiftCode { get; set; }

    public string? Currency { get; set; } = null!;

    public Country CountryCode { get; set; } = Country.Other;

    public bool IsActive { get; set; } = true;

    public bool IsEmailVerified { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<User> Users { get; set; } = [];

    public ICollection<Payout> Payouts { get; set; } = [];

    public ICollection<Coupon> Coupons { get; set; } = [];

    public ICollection<PartnerRefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<PartnerVerificationCode> VerificationCodes { get; set; } = [];
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