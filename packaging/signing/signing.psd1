@{
    # Leave blank to discover signtool.exe from PATH or a Windows 10/11 SDK installation.
    SignToolPath = ""

    # Prefer a CurrentUser certificate-store thumbprint for unattended signing.
    CertificateThumbprint = ""
    CertificateThumbprintEnvironmentVariable = "DANS_RBI_SIGNING_CERTIFICATE_THUMBPRINT"
    CertificateStoreName = "My"
    CertificateStoreLocation = "CurrentUser"

    # A PFX is also supported. The password is read only from the named environment variable.
    CertificatePath = ""
    CertificatePathEnvironmentVariable = "DANS_RBI_SIGNING_CERTIFICATE_PATH"
    CertificatePasswordEnvironmentVariable = "DANS_RBI_SIGNING_CERTIFICATE_PASSWORD"

    TimestampUrl = "http://timestamp.digicert.com"
    DigestAlgorithm = "SHA256"
}
