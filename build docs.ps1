msbuild tools\BuildDocPages\BuildDocPages.sln /verbosity:minimal

msbuild  /t:xsd /t:NuGetSchemaPackage /t:NuGetConfigPackage .\src\NLog.proj /p:Configuration=Release /p:BuildNetFX45=true /p:BuildVersion=$versionProduct /p:Configuration=Release /p:BuildLabelOverride=NONE /verbosity:minimal

$versionProduct = "4.6.0";

build\bin\tools\BuildDocPages.exe src\nlog\bin\Release\net45\API\NLog.api tools\WebsiteFiles\markdown.xsl "build\docs2" . md web


