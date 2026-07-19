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
        var code = RandomNumberGenerator
            .GetInt32(100000, 1000000)
            .ToString();

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

        await emailSender.SendAsync(
            partner.Email,
            "Verify your email — NEXUS Publisher Portal",
            BuildOtpEmailHtml(verificationCode, partner.Email),
            cancellationToken);
    }


    private static string BuildOtpEmailHtml(PartnerVerificationCode verificationCode, string email)
    {
        var digits = string.Join("</td><td>", verificationCode.Code.ToCharArray());

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
                             color:#111827;">Verify your email address</h1>
                  <p style="margin:0;color:#6b7280;font-size:15px;line-height:1.6;">
                    Use the code below to complete your<br>Publisher Portal registration.
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
                          ⏰ This code expires in <strong>{verificationCode.ExpiresAt.Subtract(DateTime.UtcNow).TotalMinutes:F0} minutes</strong>
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
                    If you didn't create a NEXUS Publisher account, you can 
                    safely ignore this email. No further action is required.
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
                    © {DateTime.UtcNow.Year} NEXUS Publisher Portal. All rights reserved.
                  </p>
                  <p style="margin:0;color:#9ca3af;font-size:11px;">
                    You are receiving this email because a registration 
                    request was initiated for {email}
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