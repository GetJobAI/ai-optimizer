FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY GetJobAI.Optimisation/GetJobAI.Optimisation.csproj GetJobAI.Optimisation/
RUN dotnet restore GetJobAI.Optimisation/GetJobAI.Optimisation.csproj

COPY GetJobAI.Optimisation/ GetJobAI.Optimisation/
RUN dotnet publish GetJobAI.Optimisation/GetJobAI.Optimisation.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GetJobAI.Optimisation.dll"]
