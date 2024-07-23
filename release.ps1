Push-Location
Set-Location $PSScriptRoot

$name = 'tubeToys'
$assembly = "Community.PowerToys.Run.Plugin.$name"
$version = "v$((Get-Content ./plugin.json | ConvertFrom-Json).Version)"
$archs = @('x64')
$tempDir = './tubeToys'

git tag $version
git push --tags

Remove-Item ./tubeToys/*.zip -Recurse -Force -ErrorAction Ignore
foreach ($arch in $archs) {
	$releasePath = "./bin/$arch/Release/net8.0-windows"

	dotnet build -c Release /p:Platform=$arch

	Remove-Item "$tempDir/*" -Recurse -Force -ErrorAction Ignore
	mkdir "$tempDir" -ErrorAction Ignore
	$items = @(
		"$releasePath/$assembly.deps.json",
		"$releasePath/$assembly.dll",
		"$releasePath/plugin.json",
		"$releasePath/Images"
	)
	Copy-Item $items "$tempDir" -Recurse -Force
	Compress-Archive "$tempDir" "./tubeToys/$name-$version-$arch.zip" -Force
}

gh release create $version (Get-ChildItem ./tubeToys/*.zip)
Pop-Location
