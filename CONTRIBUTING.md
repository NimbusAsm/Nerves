# Contributing

## Ground rules

1. **Sign off every commit** (`git commit -s`) — we use DCO, not a CLA.
2. **Conventional Commits**, no emoji prefixes:
   `<type>(<scope>): <subject>` — e.g. `feat(identity): add legal entity aggregate`.
3. **Trunk-based**: branch off `main`, keep branches under a week, squash merge.
   The PR title becomes the commit message, so it must follow the convention too.
4. **Contracts first**: change `proto/` or the OpenAPI surface, regenerate, then implement.
5. **No new dependency without a justification** in the PR description.

## Local setup

```bash
dotnet build
dotnet test
```

## Security-sensitive changes

Anything touching authentication or authorization needs two reviewers,
one of them a maintainer. See `SECURITY.md`.
