# centaurus-ban-extension
This extension automatically bans clients for repeated rules violations

**This extension requires next parameters:**
- **`connectionString`** (`string`) - Connection string for MongoDB.
- **`singleBanPeriod`** (`Int32`) - Single ban period in seconds.
- **`banPeriodMultiplier`** (`Int32`) - Ban period multiplier.

**Current extension config example:**
```
    ...
    {
        "name": "Centaurus.BanExtension",
        "extensionConfig": { 
            "connectionString": "mongodb://localhost:27001/testDB?replicaSet=centaurusTest",
            "singleBanPeriod": 10,
            "banPeriodMultiplier": 10
    },
    ...
```