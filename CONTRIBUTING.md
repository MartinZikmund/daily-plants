# Contributing to Daily Dozen

Thank you for your interest in contributing to Daily Dozen! This document provides guidelines and information for contributors.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [How to Contribute](#how-to-contribute)
- [Development Setup](#development-setup)
- [Coding Guidelines](#coding-guidelines)
- [Pull Request Process](#pull-request-process)
- [Reporting Issues](#reporting-issues)

## Code of Conduct

This project adheres to a [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/your-username/daily-dozen.git
   cd daily-dozen
   ```
3. **Add the upstream remote**:
   ```bash
   git remote add upstream https://github.com/user/daily-dozen.git
   ```
4. **Create a branch** for your changes:
   ```bash
   git checkout -b feature/your-feature-name
   ```

## How to Contribute

### Types of Contributions

We welcome the following types of contributions:

- **Bug fixes** - Fix issues and improve stability
- **New features** - Add new functionality (please discuss first)
- **Documentation** - Improve README, code comments, or wiki
- **Translations** - Add or improve language translations
- **Tests** - Add unit tests or improve test coverage
- **Performance** - Optimize code for better performance
- **Accessibility** - Improve accessibility for all users

### Before You Start

- **Check existing issues** to see if your idea is already being discussed
- **Open an issue first** for significant changes to discuss the approach
- **Keep changes focused** - one feature or fix per pull request

## Development Setup

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (17.8+) with Uno Platform extension
- Git

### Building the Project

```bash
cd src/DailyDozen
dotnet restore
dotnet build
```

### Running Tests

```bash
cd src/DailyDozen.Tests
dotnet test
```

### Running the App

```bash
# Windows
dotnet run -f net9.0-windows10.0.26100

# WebAssembly (for quick testing)
dotnet run -f net9.0-browserwasm
```

## Coding Guidelines

### General Principles

- **Keep it simple** - Write clear, readable code
- **Follow existing patterns** - Match the style of surrounding code
- **Document why, not what** - Comments should explain reasoning, not obvious actions
- **Test your changes** - Ensure existing tests pass and add new tests for new code

### C# Style

- Use C# 12 features where appropriate
- Follow [.NET naming conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use `var` when the type is obvious from the right side
- Prefer expression-bodied members for simple properties and methods
- Use nullable reference types (`#nullable enable`)

### XAML Style

- Use meaningful `x:Name` values
- Group related properties together
- Use resource dictionaries for reusable styles
- Follow the existing indentation (4 spaces)

### Project Structure

```
src/DailyDozen/
├── Models/        # Data models and entities
├── ViewModels/    # MVVM ViewModels
├── Views/         # XAML pages and user controls
├── Services/      # Business logic and data access
├── Controls/      # Reusable custom controls
├── Converters/    # XAML value converters
├── Helpers/       # Utility classes
├── Strings/       # Localization resources
└── Platforms/     # Platform-specific code
```

### Commit Messages

Write clear, concise commit messages:

- Use the imperative mood ("Add feature" not "Added feature")
- Keep the first line under 72 characters
- Reference issues when applicable (`Fixes #123`)

Examples:
```
feat: Add weight tracking export to CSV
fix: Correct streak calculation for skipped days
docs: Update build instructions for macOS
refactor: Extract checklist validation to service
```

## Pull Request Process

### Before Submitting

1. **Update your branch** with the latest upstream changes:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```
2. **Run all tests** and ensure they pass
3. **Test on multiple platforms** if possible (at minimum: Windows and WebAssembly)
4. **Update documentation** if your changes affect user-facing features

### Submitting Your PR

1. **Push your branch** to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```
2. **Open a Pull Request** on GitHub
3. **Fill out the PR template** completely
4. **Link related issues** using keywords (e.g., "Fixes #123")

### PR Review Process

- Maintainers will review your PR and may request changes
- Address feedback by pushing additional commits
- Once approved, a maintainer will merge your PR
- Your contribution will be acknowledged in the release notes

### What We Look For

- Code follows the project's style and patterns
- Changes are well-tested
- Documentation is updated if needed
- PR description clearly explains the changes
- No unrelated changes are included

## Reporting Issues

### Bug Reports

When reporting bugs, please include:

- **Description** - Clear description of the issue
- **Steps to reproduce** - Detailed steps to reproduce the behavior
- **Expected behavior** - What you expected to happen
- **Actual behavior** - What actually happened
- **Environment** - OS, .NET version, app version
- **Screenshots** - If applicable

### Feature Requests

For feature requests, please include:

- **Problem statement** - What problem does this solve?
- **Proposed solution** - How would you like it to work?
- **Alternatives considered** - Other approaches you've thought about
- **Additional context** - Any other relevant information

## Localization

We welcome translations! To add a new language:

1. Create a new folder under `src/DailyDozen/Strings/` with the language code (e.g., `de` for German)
2. Copy `en/Resources.resw` to your new folder
3. Translate all strings in the file
4. Test the app with your language
5. Submit a PR with your translation

## Questions?

If you have questions about contributing, feel free to:

- Open a [Discussion](https://github.com/user/daily-dozen/discussions) on GitHub
- Ask in an existing issue if related
- Reach out to the maintainers

Thank you for contributing to Daily Dozen!
