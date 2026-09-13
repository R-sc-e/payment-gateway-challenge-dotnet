FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /source

COPY PaymentGateway.sln ./
COPY src/PaymentGateway.Api/PaymentGateway.Api.csproj src/PaymentGateway.Api/
COPY test/PaymentGateway.Api.Tests/PaymentGateway.Api.Tests.csproj test/PaymentGateway.Api.Tests/
RUN dotnet restore PaymentGateway.sln

COPY . .

FROM restore AS test
RUN dotnet test PaymentGateway.sln \
    --configuration Release \
    --no-restore \
    /p:CollectCoverage=true \
    /p:CoverletOutput=/coverage/coverage.cobertura.xml \
    /p:CoverletOutputFormat=cobertura \
    /p:Threshold=85 \
    /p:ThresholdType=line%2cbranch \
    /p:ThresholdStat=total

FROM restore AS publish
RUN dotnet publish src/PaymentGateway.Api/PaymentGateway.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=publish --chown=$APP_UID:$APP_UID /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
