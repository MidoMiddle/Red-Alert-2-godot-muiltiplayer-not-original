# The relay server (docs/implementation/M9_network.md, 4.14, step 12). From the root of the repository:
#   docker build -f src/Rts.Server/Dockerfile -t rts-relay .
#   docker run -d --name rts-relay --restart unless-stopped -p 27020:27020/udp rts-relay
# Only the projects the relay is built from go in; global.json stays out, so any .NET 10 SDK of the image builds it.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props Directory.Build.targets BannedSymbols.txt ./
COPY src/Rts.Analyzers/ src/Rts.Analyzers/
COPY src/Rts.Simulation/ src/Rts.Simulation/
COPY src/Rts.Net/ src/Rts.Net/
COPY src/Rts.Server/ src/Rts.Server/
RUN dotnet publish src/Rts.Server/Rts.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
# The image's own user without rights: the relay needs none.
USER app
EXPOSE 27020/udp
ENTRYPOINT ["dotnet", "Rts.Server.dll"]
CMD ["--port", "27020"]
