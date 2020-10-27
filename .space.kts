job("Run build") {
    container("mcr.microsoft.com/dotnet/sdk:5.0") {
        resources {
            cpu = 2048
            memory = 2048
        }

        mountDir = "/mnt/mySpace"
        workDir = "/mnt/mySpace/work"
        user = "root"
        
        shellScript {
            content = """            
            	dotnet restore
                dotnet build
            """
        }
    }
}
