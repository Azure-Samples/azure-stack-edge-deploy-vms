Function Set-Login
{
    [cmdletbinding()]
    Param 
    (
        [string]$baseUrl,
        [string]$password,
        [string]$newPassword
    )
    
    Process 
    {
        # Trust self-signed cert
        if ("TrustAllCertsPolicy" -as [type]) {} else {
            Add-Type -ErrorAction Continue "
                    using System.Net; 
                    using System.Security.Cryptography.X509Certificates;
                    public class TrustAllCertsPolicy : ICertificatePolicy {
                    public bool CheckValidationResult(
                    ServicePoint srvPoint, X509Certificate certificate,
                    WebRequest request, int certificateProblem) {
                    return true;
                 }
             }
            "
        }
        [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy

        # Invoke login request
        # Storing ASP session cookie and initial anti-forgery token (RequestVerificationToken) in $Session and $headers
        if ($newPassword -ne $null)
        {
            $credentials = @{ Password = $password; NewPassword = $newPassword; NewPassword2 = $newPassword } | ConvertTo-Json
        }
        else
        {
            $credentials = @{ Password = $password } | ConvertTo-Json
        }
        $login = Invoke-WebRequest "$($baseUrl)/api/Login" -Method Post -Body $credentials -ContentType "application/json" -SessionVariable 'Session' -UseBasicParsing
        $token = $login.Headers["RequestVerificationToken"]
        $headers = @{ 'RequestVerificationToken' = $token }

        # Store $Session and $Headers as globals so that we can use them in the other functions easily
        Set-Variable -Name Session -Value $Session -Scope global
        Set-Variable -Name Headers -Value $headers -Scope global
        Set-Variable -Name BaseUrl -Value $baseUrl -Scope global
    }
}

Function Get-DeviceConfiguration
{
    [cmdletbinding()]
    Param 
    (
    )
    
    Process 
    {
        # Session and BaseUrl are populated in Set-Login

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration" -WebSession $Session
    }
}

Function Set-DeviceConfiguration
{
    [cmdletbinding()]
    Param 
    (
        [object] $desiredDeviceConfig
    )
    
    Process 
    {
        # Session, Headers and BaseUrl are populated in Set-Login
    
        $body = $desiredDeviceConfig | ConvertTo-Json -Depth 10

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration" -Method Post -WebSession $Session -Body $body -Headers $Headers -ContentType "application/json"
    }
}

Function Get-DeviceConfigurationStatus
{
    [cmdletbinding()]
    Param 
    (
    )
    
    Process 
    {
        # Session and BaseUrl are populated in Set-Login

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration/Status" -WebSession $Session
    }
}

Function Get-DeviceDiagnostic
{
    [cmdletbinding()]
    Param 
    (
    )
    
    Process 
    {
        # Session and BaseUrl are populated in Set-Login

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration/diagnostic" -WebSession $Session
    }
}

Function Start-DeviceDiagnostic
{
    [cmdletbinding()]
    Param 
    (
    )
    
    Process 
    {
        # Session and BaseUrl are populated in Set-Login

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration/diagnostic" -Method Post -WebSession $Session -Headers $Headers
    }
}

Function Get-DeviceLogConsent
{
    [cmdletbinding()]
    Param
    (
    )

    Process
    {
        # Session and BaseUrl are populated in Set-Login

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration/supportConsent" -WebSession $Session
    }
}

Function Set-DeviceLogConsent
{
    [cmdletbinding()]
    Param
    (
        [bool] $logConsent
    )

    Process
    {
        # Session, Headers and BaseUrl are populated in Set-Login

        $body = $logConsent | ConvertTo-Json

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration/supportConsent" -Method Post -WebSession $Session -Body $body -Headers $Headers -ContentType "application/json"
    }
}


Function Get-DeviceVip
{
    [cmdletbinding()]
    Param
    (
    )

    Process
    {
        # Session and BaseUrl are populated in Set-Login

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration/vip" -WebSession $Session
    }
}

Function Set-DeviceVip
{
    [cmdletbinding()]
    Param
    (
        [object] $vip
    )

    Process
    {
        # Session, Headers and BaseUrl are populated in Set-Login

        $body = $vip | ConvertTo-Json -Depth 10

        Invoke-RestMethod "$($BaseUrl)/api/DeviceConfiguration/vip" -Method Post -WebSession $Session -Body $body -Headers $Headers -ContentType "application/json"
    }
}

Function New-Package
{
    [cmdletbinding()]
    Param 
    (
        $activation,
        $certificates,
        $deviceEndpoint,
        $encryptionAtRestKeys,
        $network,
        $password,
        $time,
        $update,
        $webProxy
    )
    
    Process 
    {
        $device = New-Object -TypeName PSObject
        
        if ($activation -ne $null)
        {
            $device | Add-Member 'activation' $activation
        }
        if ($certificates -ne $null)
        {
            $device | Add-Member 'certificates' $certificates
        }
        if ($deviceEndpoint -ne $null)
        {
            $device | Add-Member 'deviceEndpoint' $deviceEndpoint
        }
        if ($encryptionAtRestKeys -ne $null)
        {
            $device | Add-Member 'encryptionAtRestKeys' $encryptionAtRestKeys
        }
        if ($network -ne $null)
        {
            $device | Add-Member 'network' $network
        }
        if ($password -ne $null)
        {
            $device | Add-Member 'password' $password
        }
        if ($time -ne $null)
        {
            $device | Add-Member 'time' $time
        }
        if ($update -ne $null)
        {
            $device | Add-Member 'update' $update
        }
        if ($webProxy -ne $null)
        {
            $device | Add-Member 'webProxy' $webProxy
        }
        
        $pkg = New-Object -TypeName PSObject
        $pkg | Add-Member 'device' $device
        
        $pkg
    }
}

Function To-Json
{
    [cmdletbinding()]
    Param 
    (
        [object]
        [parameter(mandatory=$true,valuefrompipeline=$true)] $o
    )
    
    Process 
    {
        $o | ConvertTo-Json -Depth 10
    }
}
