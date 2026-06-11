FROM ://microsoft.com AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM ://microsoft.com
WORKDIR /app
COPY --from=build /app .
EXPOSE 5005/udp
ENTRYPOINT ["dotnet", "NatCoordinator.dll"]
