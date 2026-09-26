.PHONY: restore build test run compose-up compose-down

restore: ; dotnet restore Acmeplatform.slnx
build:   ; dotnet build Acmeplatform.slnx -c Release
test:    ; dotnet test Acmeplatform.slnx
run:     ; dotnet run --project AppHost/AppHost.csproj   # Aspire: one command, whole system
compose-up:   ; docker compose up -d
compose-down: ; docker compose down -v
