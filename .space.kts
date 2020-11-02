job("Run build") {
    container("mcr.microsoft.com/dotnet/sdk:5.0") {
        resources {
            cpu = 2048
            memory = 2048
        }
        
        shellScript {
            content = """
            	dotnet restore
                dotnet build BookSomeSpace.sln -c Release
                dotnet publish -c Release -r linux-x64 --self-contained true --output /mnt/space/share/app
            """
        }
    }
}