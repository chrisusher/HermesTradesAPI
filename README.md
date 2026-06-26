# HermesTradesAPI

HermesTradesAPI is a modern .NET 10 workspace for building and operating a trading-focused backend with a clean service layer, an Azure Functions API, and a command-line interface. It blends portfolio, strategy, transaction, reporting, and user workflows into a single, extensible platform.

What makes it interesting is the balance between developer ergonomics and real-world backend concerns: a structured service architecture, API exposure through Azure Functions, and a first-class local orchestration experience with .NET Aspire.

## What’s inside

- A backend powered by Azure Functions and OpenAPI/Swagger
- A shared service layer for portfolios, strategies, transactions, reporting, and users
- A CLI for operational and infrastructure-oriented commands
- An Aspire-based local development experience for running and inspecting the stack together

## Architecture at a glance

- Backend: Azure Functions host with HTTP endpoints and Swagger support
- Services: domain logic for trading-related workflows and persistence
- Shared: DTOs, contracts, enums, and cross-cutting utilities
- CLI: lightweight command-line entry point for operational tasks

## Quick start

### Prerequisites

- .NET 10 SDK
- An Azure environment if you want to exercise cloud-backed pieces
- The Aspire CLI if you want the full dashboard experience

### Run the full local stack with Aspire

From the repository root:

```bash
aspire start
```

That launches the local development experience for the app host and surfaces the app in the Aspire dashboard. If you’re using the local backend endpoint, Swagger is typically available at:

```text
http://localhost:7287/api/swagger/ui
```

### Run the projects directly

If you want to work with individual pieces:

```bash
dotnet run --project Backend
dotnet run --project CLI
```

## Project layout

- [apphost.cs](apphost.cs) — Aspire app host entry point
- [Backend](Backend) — Azure Functions API host
- [CLI](CLI) — command-line tooling
- [Services](Services) — business logic and integrations
- [Shared](Shared) — shared contracts and common utilities

## Aspire notes

Aspire is the glue that makes local development feel cohesive. Instead of manually wiring up and monitoring a handful of moving parts, the Aspire app host gives you a structured way to run, inspect, and debug the solution as a single experience.

Use it when you want:

- a single entry point for local development
- an easy way to inspect service health and logs
- a smoother path from code to running app without losing visibility

## Configuration

The solution uses a combination of:

- app settings files for local configuration
- environment variables for deployment and runtime overrides
- optional local environment files for CLI workflows

## Development mindset

This repo is designed to be practical first and polished second: clear boundaries between API, services, and tooling, with enough structure to grow without becoming brittle.

If you’re jumping in, start with the backend and services layer, then use the CLI or Aspire dashboard to explore how the pieces fit together.
