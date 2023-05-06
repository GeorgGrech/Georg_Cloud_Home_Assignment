FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build-env
WORKDIR /app

COPY Georg_Cloud_Home_Assignment/Georg_Cloud_Home_Assignment.csproj Georg_Cloud_Home_Assignment/
COPY Common/Common.csproj Common/
RUN dotnet restore Georg_Cloud_Home_Assignment/Georg_Cloud_Home_Assignment.csproj

COPY . ./
RUN dotnet publish Georg_Cloud_Home_Assignment -c Release -o out
 
FROM mcr.microsoft.com/dotnet/aspnet:3.1
WORKDIR /app
EXPOSE 80
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "Georg_Cloud_Home_Assignment.dll"]