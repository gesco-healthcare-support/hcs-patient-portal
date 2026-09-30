#!/usr/bin/env pwsh
# Diagnostic: send the EXACT "Appointment Requested - <AppNo>" email body
# that the SendAppointmentEmailJob produces, to a fresh test address.
# Goal: confirm whether the production SMTP send path delivers this
# specific subject + body to a different mailbox (eliminating the
# "code is broken" hypothesis from the Four/Five no-receive bug).
#
# Reads the SMTP creds from docker/appsettings.secrets.json so the test
# matches the runtime path 1:1.

param(
  [Parameter(Mandatory=$true)][string]$To,
  [string]$RoleLabel = "applicant attorney",
  [string]$ConfirmationNumber = "TEST-001",
  [string]$AppointmentDateLine = "May 28, 2026 11:00 AM"
)

$ErrorActionPreference = 'Stop'

$secretsPath = Join-Path $PSScriptRoot "..\..\docker\appsettings.secrets.json"
if (-not (Test-Path $secretsPath)) {
  Write-Error "Missing $secretsPath; cannot read SMTP creds."
}
$secrets = Get-Content $secretsPath -Raw | ConvertFrom-Json
$s = $secrets.Settings
$smtpHost = $s.'Abp.Mailing.Smtp.Host'
$smtpPort = [int]$s.'Abp.Mailing.Smtp.Port'
$smtpUser = $s.'Abp.Mailing.Smtp.UserName'
$smtpPass = $s.'Abp.Mailing.Smtp.Password'
$fromAddr = $s.'Abp.Mailing.DefaultFromAddress'
$fromName = $s.'Abp.Mailing.DefaultFromDisplayName'

$subject = "Appointment Requested - $ConfirmationNumber"

$body = @"
<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head>
    <meta http-equiv="Content-Type" content="text/html;charset=utf-8" />
</head>
<body style="font-family: Arial, Helvetica, sans-serif; font-size: 14px; line-height: 20px; color: #000;">
    <h2 style="color: #0d6efd;">An appointment was requested</h2>
    <p>Confirmation #<strong>$ConfirmationNumber</strong> requested for <strong>$AppointmentDateLine</strong>.</p>
    <p>
        <strong>Booker:</strong> Test PatientThree<br />
        <strong>Patient:</strong> Synthetic TestPatient
    </p>
    <p>You are listed as the <strong>$RoleLabel</strong> on this appointment. Log in to the patient portal to view the request, see updates, and receive scheduling notifications.</p>
    <p style="margin-top: 20px;">
        <a href="http://falkinstein.localhost:4200" style="background:#0d6efd;color:#fff;padding:10px 20px;text-decoration:none;border-radius:4px;">
            Open patient portal
        </a>
    </p>
</body>
</html>
"@

Write-Host "Sending diagnostic email:" -ForegroundColor Cyan
Write-Host "  Host:    $smtpHost`:$smtpPort (STARTTLS)"
Write-Host "  From:    $fromName <$fromAddr>"
Write-Host "  To:      $To"
Write-Host "  Subject: $subject"
Write-Host "  RoleLbl: $RoleLabel"
Write-Host ""

# Use System.Net.Mail.SmtpClient with STARTTLS (EnableSsl on port 587)
# Side note: ABP uses MailKit but System.Net.Mail also implements STARTTLS
# on port 587 with EnableSsl = $true. Either reaches the same relay.
$mail = New-Object System.Net.Mail.MailMessage
$mail.From = New-Object System.Net.Mail.MailAddress($fromAddr, $fromName)
$mail.To.Add($To)
$mail.Subject = $subject
$mail.Body = $body
$mail.IsBodyHtml = $true

$smtp = New-Object System.Net.Mail.SmtpClient($smtpHost, $smtpPort)
$smtp.EnableSsl = $true
$smtp.Credentials = New-Object System.Net.NetworkCredential($smtpUser, $smtpPass)
$smtp.DeliveryMethod = [System.Net.Mail.SmtpDeliveryMethod]::Network

try {
  $smtp.Send($mail)
  Write-Host "SMTP send returned without exception." -ForegroundColor Green
  Write-Host "Check inbox $To for subject: $subject"
} catch {
  Write-Host "SMTP send FAILED:" -ForegroundColor Red
  Write-Host $_.Exception.Message
  if ($_.Exception.InnerException) {
    Write-Host "Inner: $($_.Exception.InnerException.Message)" -ForegroundColor Red
  }
  exit 1
} finally {
  $smtp.Dispose()
  $mail.Dispose()
}
