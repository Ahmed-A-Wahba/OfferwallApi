using System.Security.Cryptography;
using OfferwallApi.Infrastructure.Interfaces;
using OfferwallApi.Infrastructure.Persistence;
using OfferwallApi.Shared.Entities;

namespace OfferwallApi.Infrastructure.Services;

public sealed class VerificationCodeService(
    ApplicationDbContext dbContext,
    IEmailSender emailSender)
    : IVerificationCodeService
{
  public async Task SendPartnerVerificationCodeAsync(
      Partner partner,
      VerificationCodeType type,
      CancellationToken cancellationToken = default)
  {
    var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    var verificationCode = new PartnerVerificationCode
    {
      VerificationCodeId = Guid.CreateVersion7(),
      PartnerId = partner.PartnerId,
      Code = code,
      Type = type,
      ExpiresAt = DateTime.UtcNow.AddMinutes(2),
      CreatedAt = DateTime.UtcNow
    };

    dbContext.PartnerVerificationCodes.Add(verificationCode);

    var subject = type switch
    {
      VerificationCodeType.EmailVerification => "Verify your email — NEXUS Publisher Portal",
      VerificationCodeType.ForgotPassword => "Reset your password — NEXUS Publisher Portal",
      _ => "Verification Code — NEXUS Publisher Portal"
    };

    await emailSender.SendAsync(
        partner.Email,
        subject,
        BuildPartnerOtpEmailHtml(verificationCode, partner.Email, type),
        cancellationToken);
  }

  public async Task SendAdminVerificationCodeAsync(
      Admin admin,
      VerificationCodeType type,
      CancellationToken cancellationToken = default)
  {
    var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

    var verificationCode = new AdminVerificationCode
    {
      VerificationCodeId = Guid.CreateVersion7(),
      AdminId = admin.AdminId,
      Code = code,
      Type = type,
      ExpiresAt = DateTime.UtcNow.AddMinutes(5),
      CreatedAt = DateTime.UtcNow
    };

    dbContext.AdminVerificationCodes.Add(verificationCode);

    var subject = type switch
    {
      VerificationCodeType.EmailVerification => "Admin Login Verification — NEXUS Operational Console",
      _ => "Verification Code — NEXUS Operational Console"
    };

    await emailSender.SendAsync(
        admin.Email,
        subject,
        BuildAdminOtpEmailHtml(verificationCode, admin.Email, type),
        cancellationToken);
  }

  private static string BuildAdminOtpEmailHtml(
          AdminVerificationCode verificationCode,
          string email,
          VerificationCodeType type)
  {
    var digits = string.Join("</td><td>", verificationCode.Code.ToCharArray());

    var (title, subtitle, requestText, footerText) = type switch
    {
      VerificationCodeType.EmailVerification => (
          Title: "Admin Access Verification",
          Subtitle: "Use the code below to securely sign in<br>to the NEXUS Operational Console.",
          RequestText: "If you didn't attempt to log in to the admin console, your credentials might be compromised. Please reset your password immediately.",
          FooterText: $"You are receiving this email because a login attempt was made for {email}"
      ),
      _ => (
          Title: "Verification Code",
          Subtitle: "Use the code below to proceed.",
          RequestText: "If you didn't request this code, you can safely ignore this email.",
          FooterText: $"You are receiving this email for {email}"
      )
    };

    return GenerateEmailTemplate(title, subtitle, requestText, footerText, digits, verificationCode.ExpiresAt, "Operational Console");
  }

  private static string BuildPartnerOtpEmailHtml(
          PartnerVerificationCode verificationCode,
          string email,
          VerificationCodeType type)
  {
    var digits = string.Join("</td><td>", verificationCode.Code.ToCharArray());

    var (title, subtitle, requestText, footerText) = type switch
    {
      VerificationCodeType.EmailVerification => (
          Title: "Partner Access Verification",
          Subtitle: "Use the code below to securely sign in<br>to the NEXUS Operational Console.",
          RequestText: "If you didn't attempt to log in to the partner console, your credentials might be compromised. Please reset your password immediately.",
          FooterText: $"You are receiving this email because a login attempt was made for {email}"
      ),
      _ => (
          Title: "Verification Code",
          Subtitle: "Use the code below to proceed.",
          RequestText: "If you didn't request this code, you can safely ignore this email.",
          FooterText: $"You are receiving this email for {email}"
      )
    };

    return GenerateEmailTemplate(title, subtitle, requestText, footerText, digits, verificationCode.ExpiresAt, "Operational Console");
  }

  private static string GenerateEmailTemplate(
      string title, string subtitle, string requestText, string footerText,
      string digits, DateTime expiresAt, string portalName)
  {
    return $"""
        <!DOCTYPE html>
        <html>
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
        </head>
        <body style="margin:0;padding:0;background:#f4f4f5;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0">
            <tr>
              <td align="center" style="padding:40px 0;">
                <table width="600" cellpadding="0" cellspacing="0" 
                       style="background:#ffffff;border-radius:12px;overflow:hidden;">
                  
                  <!-- Header -->
                  <tr>
                    <td style="background:#0f0f1a;padding:28px;text-align:center;">
                      <span style="font-size:28px;font-weight:900;color:#6d28d9;
                                   letter-spacing:2px;">NEXUS</span>
                      <br>
                      <div style="width:60px;height:2px;background:#6d28d9;
                                  margin:8px auto 0;"></div>
                    </td>
                  </tr>

                  <!-- Hero -->
                  <tr>
                    <td style="padding:40px 40px 0;text-align:center;">
                      <div style="width:64px;height:64px;background:#f0ebff;
                                  border-radius:50%;margin:0 auto 24px;
                                  display:flex;align-items:center;justify-content:center;">
                        <span style="font-size:28px;">🔒</span>
                      </div>
                      <h1 style="margin:0 0 12px;font-size:24px;font-weight:700;
                                 color:#111827;">{title}</h1>
                      <p style="margin:0;color:#6b7280;font-size:15px;line-height:1.6;">
                        {subtitle}
                      </p>
                    </td>
                  </tr>

                  <!-- OTP Box -->
                  <tr>
                    <td style="padding:32px 40px;">
                      <table width="100%" cellpadding="0" cellspacing="0"
                             style="background:#f9fafb;border:2px solid #e5e7eb;
                                    border-radius:12px;">
                        <tr>
                          <td style="padding:24px;text-align:center;">
                            <p style="margin:0 0 16px;font-size:11px;font-weight:700;
                                      color:#6b7280;letter-spacing:2px;">
                              YOUR VERIFICATION CODE
                            </p>
                            <table cellpadding="0" cellspacing="0" 
                                   style="margin:0 auto;">
                              <tr style="font-size:42px;font-weight:900;
                                         color:#6d28d9;letter-spacing:12px;">
                                <td>{digits}</td>
                              </tr>
                            </table>
                            <p style="margin:16px 0 0;color:#6b7280;font-size:13px;">
                              ⏰ This code expires in <strong>{expiresAt.Subtract(DateTime.UtcNow).TotalMinutes:F0} minutes</strong>
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>

                  <!-- Divider -->
                  <tr>
                    <td style="padding:0 40px;">
                      <hr style="border:none;border-top:1px solid #e5e7eb;">
                    </td>
                  </tr>

                  <!-- Didn't request -->
                  <tr>
                    <td style="padding:24px 40px;">
                      <p style="margin:0 0 8px;font-size:14px;font-weight:700;
                                 color:#111827;">Didn't request this?</p>
                      <p style="margin:0;color:#6b7280;font-size:14px;line-height:1.6;">
                        {requestText}
                      </p>
                    </td>
                  </tr>

                  <!-- Security Notice -->
                  <tr>
                    <td style="padding:0 40px 32px;">
                      <table width="100%" cellpadding="0" cellspacing="0"
                             style="background:#eff6ff;border-left:3px solid #6d28d9;
                                    border-radius:6px;">
                        <tr>
                          <td style="padding:16px;">
                            <p style="margin:0;font-size:13px;color:#374151;
                                      line-height:1.6;">
                              🔒 <strong>Security Notice:</strong> For security, 
                              never share this code with anyone. NEXUS will never 
                              ask for your OTP via phone or chat.
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>

                  <!-- Footer -->
                  <tr>
                    <td style="background:#f9fafb;border-top:1px solid #e5e7eb;
                               padding:24px 40px;text-align:center;">
                      <p style="margin:0 0 8px;color:#6b7280;font-size:12px;">
                        © {DateTime.UtcNow.Year} NEXUS {portalName}. All rights reserved.
                      </p>
                      <p style="margin:0;color:#9ca3af;font-size:11px;">
                        {footerText}
                      </p>
                    </td>
                  </tr>

                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
  }
}