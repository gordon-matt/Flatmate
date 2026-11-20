param(
	[Parameter(Mandatory = $true)]
	[string]$ApiKey,
	
	[Parameter(Mandatory = $true)]
	[string]$Version
)

$Source = "https://api.nuget.org/v3/index.json"

$Packages = @(
    "Flatmate"
    "Flatmate.EntityFrameworkCore"
)

foreach ($Package in $Packages) {
    $Project = "$Package\$Package.csproj"
    $PackageFile = "$Package\bin\Release\$Package.$Version.nupkg"

    Write-Host "`nPacking $Package..."
    dotnet pack $Project -c Release -p:PackageVersion=$Version

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to pack $Package"
    }

    if (-not (Test-Path $PackageFile)) {
        throw "Package not found: $PackageFile"
    }

    Write-Host "Pushing $PackageFile..."

    dotnet nuget push $PackageFile --api-key $ApiKey --source $Source

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to push $Package"
    }
}

Write-Host "`nAll packages packed and pushed successfully!"