# Build từ gốc repo: docker build -f src/SmartHomeAI.Hub/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/SmartHomeAI.Core/*.csproj src/SmartHomeAI.Core/
COPY src/SmartHomeAI.Hub/*.csproj src/SmartHomeAI.Hub/
RUN dotnet restore src/SmartHomeAI.Hub
COPY src/SmartHomeAI.Core src/SmartHomeAI.Core
COPY src/SmartHomeAI.Hub src/SmartHomeAI.Hub
COPY infra/sql infra/sql
RUN dotnet publish src/SmartHomeAI.Hub -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Python + tapo cho cầu nối ổ cắm P100 (src/tapo_p100/bridge.py), cùng phiên bản với .venv trên máy.
RUN apt-get update && apt-get install -y --no-install-recommends python3 python3-venv \
    && rm -rf /var/lib/apt/lists/* \
    && python3 -m venv /opt/venv && /opt/venv/bin/pip install --no-cache-dir tapo==0.13.0
WORKDIR /app
COPY --from=build /out .
COPY src/tapo_p100 src/tapo_p100
ENV ASPNETCORE_URLS=http://+:5002 TapoP100__Root=/app TapoP100__Python=/opt/venv/bin/python
EXPOSE 5002
HEALTHCHECK --interval=10s --retries=6 CMD ["/opt/venv/bin/python", "-c", "import urllib.request as u; u.urlopen('http://localhost:5002/health', timeout=3)"]
ENTRYPOINT ["dotnet", "SmartHomeAI.Hub.dll"]
