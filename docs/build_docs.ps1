# if pdftex or pdflatex is not installed, error out
if (-not (Get-Command pdflatex -ErrorAction SilentlyContinue)) {
    Write-Error "pdflatex is not installed. Please install a LaTeX distribution that includes pdflatex."
    exit 1
}

# for each .tex file in the current directory, compile it to pdf using pdflatex into the output directory
$texFiles = Get-ChildItem -Path . -Filter *.tex
$outputDir = "output"
if (-not (Test-Path -Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}
foreach ($texFile in $texFiles) {
    $outputPdf = Join-Path -Path $outputDir -ChildPath ($texFile.BaseName + ".pdf")
    pdflatex -output-directory=$outputDir $texFile.FullName
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to compile $($texFile.Name)"
    } else {
        Write-Host "Successfully compiled $($texFile.Name) to $outputPdf"
    }
}