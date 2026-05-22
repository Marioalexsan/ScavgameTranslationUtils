import subprocess
import zipfile
import os

rids = ["win-x64", "linux-x64"]

for rid in rids:
  subprocess.call(' '.join(
    [
      "dotnet publish ./ScavgameTranslationUtils/ScavgameTranslationUtils.csproj",
      f"--runtime {rid}",
      "--configuration Release",
      f"--output bin/publish/ScavgameTranslationUtils-{rid}/",
      "--self-contained",
      "--p:PublishTrimmed=true",
      "--p:PublishSingleFile=true"
    ]
  ), text=True)

  with zipfile.ZipFile(f"./bin/publish/ScavgameTranslationUtils-{rid}.zip", "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as f:
    for file in os.listdir(f"bin/publish/ScavgameTranslationUtils-{rid}/"):
      if not file.endswith(".pdb"):
        f.write(f"bin/publish/ScavgameTranslationUtils-{rid}/{file}", arcname=file)