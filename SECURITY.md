# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.x.x   | :white_check_mark: |

## Reporting a Vulnerability

We take security seriously. If you discover a security vulnerability in Daily Dozen, please report it responsibly.

### How to Report

**Please do NOT report security vulnerabilities through public GitHub issues.**

Instead, please report them via one of the following methods:

1. **GitHub Security Advisories** (Preferred)
   - Go to the [Security tab](https://github.com/user/daily-dozen/security/advisories)
   - Click "Report a vulnerability"
   - Fill out the form with details

2. **Email**
   - Send an email to: [security contact email]
   - Use a descriptive subject line
   - Include details as described below

### What to Include

Please include the following information in your report:

- **Description** of the vulnerability
- **Steps to reproduce** the issue
- **Affected versions** of the app
- **Potential impact** of the vulnerability
- **Suggested fix** (if you have one)

### What to Expect

- **Acknowledgment**: We will acknowledge receipt of your report within 48 hours
- **Assessment**: We will assess the vulnerability and determine its severity
- **Updates**: We will keep you informed of our progress
- **Resolution**: We aim to resolve critical issues within 7 days
- **Credit**: We will credit you in the release notes (unless you prefer anonymity)

### Scope

This security policy applies to:

- The Daily Dozen application source code
- Official releases on app stores
- The project's build and deployment infrastructure

### Out of Scope

The following are generally out of scope:

- Vulnerabilities in third-party dependencies (report to the respective projects)
- Social engineering attacks
- Physical attacks
- Denial of service attacks

## Security Best Practices

### For Users

- **Keep the app updated** to the latest version
- **Download only from official sources** (GitHub releases, official app stores)
- **Review export files** before sharing them (they contain your personal data)

### For Contributors

- **Never commit secrets** (API keys, passwords, certificates)
- **Use environment variables** for sensitive configuration
- **Review dependencies** before adding them
- **Follow secure coding practices**

## Data Security

Daily Dozen is designed with privacy in mind:

- **All data is stored locally** on your device
- **No data is transmitted** to external servers
- **No analytics or tracking** is implemented
- **Export files are not encrypted** - users should handle them securely

## Dependency Management

We use automated tools to monitor dependencies for known vulnerabilities:

- Dependabot for GitHub dependency updates
- Regular manual review of dependency security advisories

## Contact

For non-security-related questions, please use:
- [GitHub Issues](https://github.com/user/daily-dozen/issues)
- [GitHub Discussions](https://github.com/user/daily-dozen/discussions)

Thank you for helping keep Daily Dozen secure!
