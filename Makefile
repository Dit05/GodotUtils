


build-debug:
	dotnet build -c Debug

build-release:
	dotnet build -c Release

publish:
	rm -rf package/*
	dotnet pack -o package
	nuget add -Source ../packages package/*.nupkg
