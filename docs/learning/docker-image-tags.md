# Fixing the Docker startup failure

## What Docker needs before it can build

A Docker image is a packaged filesystem containing a program and its runtime dependencies. A container is a running instance of an image. A Dockerfile describes how to build our own image, starting from an existing base image.

The API Dockerfile has two stages:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0.403-alpine3.24 AS build
```

`FROM` chooses the base image. `mcr.microsoft.com` is Microsoft's container registry. `dotnet/sdk` is the image repository, containing the tools needed to compile C#. The text after the colon is the exact image tag. Here it specifies SDK 10.0.403 on Alpine Linux 3.24. `AS build` names this stage so the next stage can copy its compiled output.

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0.14-alpine3.24 AS runtime
```

The runtime stage contains what is needed to run the API, without the SDK's compiler tools. Keeping build tools out of the final image reduces unnecessary contents.

## The mistake and the correction

The original references were `10.0.403-alpine` and `10.0.14-alpine`. Neither exact tag exists in Microsoft's registry: both manifest requests returned HTTP 404. Docker cannot infer that we mean a particular Alpine version. A missing base image prevents the build before our API can start.

The replacement references explicitly name `alpine3.24`. Verify the image registry publishes each exact reference, including a Linux/amd64 variant for the GitHub runner, and validate the Compose configuration. The fix changes the image references; it does not alter C# application code, database schema or frontend behavior.

## Why the other jobs could pass

The API job installs the .NET SDK directly, and the frontend job installs Node.js directly. Neither uses the API Dockerfile. The Compose job builds the API's container image, exposing an error that the other jobs do not exercise.

This is why we keep both application tests and a full-stack startup check. They answer different questions: does the code work, and can another machine package and start all of it together?

## How to confirm the full fix

After the fix is pushed to GitHub, the Checks workflow must run on that new commit. The Compose job should build the images, start PostgreSQL, apply the migration, start the API and frontend, and pass its HTTP checks. Valid registry references and valid YAML are necessary checks, but they do not by themselves prove the complete stack started.

The current machine has no Docker engine. Full container verification therefore depends on the new GitHub run. Reading detailed logs also requires an authenticated GitHub session, which is not currently available to the terminal.

## Try reading the file yourself

Open `src/RushCtrl.Api/Dockerfile`. Locate the two `FROM` lines, then locate `COPY --from=build`. The copy takes the compiled application from the build stage into the runtime stage. Follow that with the final `ENTRYPOINT`: it tells the container to run `dotnet RushCtrl.Api.dll`.
