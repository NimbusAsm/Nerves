# About

Nerves is a nice user account system powered by dotnet platform.

## Structure

- Api Server: `dotnet`
- Data Base: `MongoDB` (More options will be available in the future)
- KV DB: `Redis` (More options will be available in the future)
- Admin dashboard: `dotnet blazor`
- Account Hub: `dotnet blazor`

# Usage

## Native Server

Todo ...

## Docker

Todo ...

## Manually

Below codes were used to fetch source code and run them in development environment.

```shell
## Fetch source code
git clone git@github.com:NebulaHub/Nerves.git
cd Nerves

## Start Api Server (/)
cd Nerves.ApiServer
dotnet watch # This will enable hot-reload, use `dotnet run` to disable it

## Start Admin Dashboard (/)
cd Nerves.Dashboard
dotnet watch # ...

## Start Account Hub (/)
cd Nerves.Hub
dotnet watch # ...
```

If you want to manually make a production configuration, do like this:

Todo ...

# Contributing

See [CONTRIBUTING.md](./.github/CONTRIBUTING.md).
