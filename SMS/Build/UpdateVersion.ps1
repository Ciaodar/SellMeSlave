param(
    [string]$SubModulePath = (Join-Path $PSScriptRoot "..\..\SubModule.xml"),
    [string]$Configuration = "Debug"
)

# Read SubModule.xml file
if (-not (Test-Path $SubModulePath)) {
    Write-Error "SubModule.xml not found: $SubModulePath"
    exit 1
}

[xml]$xml = Get-Content $SubModulePath

# Get current version
$currentVersion = $xml.Module.Version.value
Write-Host "Current version: $currentVersion"

# Version format: v1.0.0.0
# Segments: v1 . x(Update) . y(Hotfix) . z(Debug)
if ($currentVersion -match '^(v\d+)\.(\d+)\.(\d+)\.(\d+)$') {
    $prefix = $matches[1]      # v1
    $update = [int]$matches[2] # x
    $hotfix = [int]$matches[3] # y
    $debug = [int]$matches[4]  # z

    if ($Configuration -eq "Update") {
        $update++
        $debug++
        $hotfix = 0
        Write-Host "Update Build: Update version incremented, Hotfix reset."
    } elseif ($Configuration -eq "Hotfix") {
        $hotfix++
        $debug++
        Write-Host "Hotfix Build: Hotfix version incremented."
    } else {
        # Debug
        $debug++
        Write-Host "Debug Build: Debug version incremented."
    }

    $newVersion = "$prefix.$update.$hotfix.$debug"
    Write-Host "New version: $newVersion"

    # Update version in XML
    $xml.Module.Version.value = $newVersion

    # Save file (preserving formatting)
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = "  "
    $settings.NewLineChars = "`n"
    $settings.Encoding = [System.Text.Encoding]::UTF8

    $writer = [System.Xml.XmlWriter]::Create($SubModulePath, $settings)
    $xml.WriteTo($writer)
    $writer.Flush()
    $writer.Close()

    Write-Host "SubModule.xml successfully updated."
    exit 0
} else {
    Write-Error "Invalid version format: $currentVersion (expected format: v1.0.0.0)"
    exit 1
}
