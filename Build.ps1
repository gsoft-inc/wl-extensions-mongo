#Requires -Version 5.0

Begin {
    $ErrorActionPreference = "stop"
}

Process {
    function Exec([scriptblock]$Command) {
        & $Command
        if ($LASTEXITCODE -ne 0) {
            throw ("An error occurred while executing command: {0}" -f $Command)
        }
    }

    $workingDir = Join-Path $PSScriptRoot "src"
    $outputDir = Join-Path $PSScriptRoot ".output"
    $nupkgsPath = Join-Path $outputDir "*.nupkg"

    try {
        Push-Location $workingDir
        Remove-Item $outputDir -Force -Recurse -ErrorAction SilentlyContinue

        # Install GitVersion which is specified in the .config/dotnet-tools.json
        # https://learn.microsoft.com/en-us/dotnet/core/tools/local-tools-how-to-use
        Exec { & dotnet tool restore }

        # Let GitVersion compute the NuGet package version
        # $shortSHA = Exec { & git rev-parse --short HEAD }
        # $gitVersion = Exec { & dotnet dotnet-gitversion /output json /showvariable SemVer }
        # $version = "$gitVersion.$shortSHA"

        Exec { & dotnet clean -c Release }
        Exec { & dotnet build -c Release }
        Exec { & dotnet test  -c Release --results-directory "$outputDir" --no-restore -l "trx" -l "console;verbosity=detailed" }
        Exec { & dotnet pack -c Release -o "$outputDir" }

        if (($null -ne $env:NUGET_SOURCE ) -and ($null -ne $env:NUGET_API_KEY)) {
            Exec { & dotnet nuget push "$nupkgsPath" -s $env:NUGET_SOURCE -k $env:NUGET_API_KEY }
        }
        Write-Host "Packing with version: $version"
    }
    finally {
        Pop-Location
    }
}
