FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AxarDB.csproj", "./"]
RUN dotnet restore "AxarDB.csproj"
COPY . .
WORKDIR /src
RUN dotnet build "AxarDB.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AxarDB.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 5000
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AxarDB.dll"]
