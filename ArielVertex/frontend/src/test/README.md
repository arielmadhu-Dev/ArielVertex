# Frontend tests

Run once:

```powershell
npm test
```

Run interactively while developing:

```powershell
npm run test:watch
```

The tests run in Vitest with jsdom and React Testing Library. Network-facing API methods are mocked;
tests do not require the backend server and do not modify application data.

Current coverage focuses on authentication state, token/error helpers, permission and feature-flag
navigation, appraisal calculations, rating accessibility, pagination, and modal keyboard behavior.
