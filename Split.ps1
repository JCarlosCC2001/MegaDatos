$lines = Get-Content -Path "ViewModels\MainViewModel.cs" -Encoding UTF8
$header = $lines[0..19]
$footer = @("}")

function Write-Part {
    param($fileName, $start, $end)
    $partLines = $header + $lines[$start..$end] + $footer
    Set-Content -Path "ViewModels\$fileName" -Value $partLines -Encoding UTF8
}

$coreLines = $header + $lines[20..63] + $footer
Set-Content -Path "ViewModels\MainViewModel.cs" -Value $coreLines -Encoding UTF8

Write-Part "MainViewModel.Explorer.cs" 64 165
Write-Part "MainViewModel.Metadata.cs" 166 207
Write-Part "MainViewModel.Tools.cs" 208 629
Write-Part "MainViewModel.State.cs" 630 904
Write-Part "MainViewModel.Commands.cs" 905 2109

# Last part has the closing brace built-in
$ioLines = $header + $lines[2110..2536]
Set-Content -Path "ViewModels\MainViewModel.IO.cs" -Value $ioLines -Encoding UTF8
