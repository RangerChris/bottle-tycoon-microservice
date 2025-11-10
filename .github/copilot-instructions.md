# Copilot Instructions for AI-Assisted Development

## Purpose
The purpose of this document is to provide comprehensive guidelines for integrating AI-assisted development into the Bottle Tycoon Microservice project. This includes utilizing AI tools to enhance development efficiency, code quality, and overall project maintainability.

## Coding Conventions
- **Language Features**: Follow the latest C# language features applicable in .NET 9.
- **Naming Conventions**: Use PascalCase for class names, methods, and properties. Use camelCase for local variables and method parameters.
- **File Organization**: Organize files by feature rather than type. Each microservice should have its own directory with subdirectories for Controllers, Services, and Models.
- **Documentation**: Every public method should be documented with XML comments. Use consistent formatting to enhance readability.

## Testing Approach
- **Unit Testing**: Use xUnit for unit testing. Each method should have corresponding unit tests covering edge cases and main scenarios.
- **Integration Testing**: Implement integration tests to verify interactions between microservices. Use TestContainers to test against real services.
- **Continuous Integration**: Set up a CI pipeline that runs tests on each pull request to maintain code quality.

## Security Measures
- **Authentication and Authorization**: Implement JWT for authentication. Use role-based access control to protect sensitive endpoints.
- **Data Validation**: Validate all incoming data to prevent injection attacks. Use built-in validation attributes where applicable.
- **Secrets Management**: Store sensitive data (like connection strings and API keys) in Azure Key Vault or similar services. Never hard-code secrets in the codebase.

## Observability Requirements
- **Logging**: Use Serilog for logging within the microservices. Ensure logs include enough context to trace issues efficiently.
- **Monitoring**: Integrate Application Insights to monitor application performance metrics and error rates.
- **Tracing**: Implement distributed tracing using OpenTelemetry to track requests across microservices for better observability.

## Conclusion
Following these guidelines will help ensure that AI-assisted development aligns with project goals and maintains high standards of code quality, security, and observability. Continuous improvements to these practices are encouraged as new tools and methods emerge.