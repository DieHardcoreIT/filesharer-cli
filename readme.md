# FileSharer-CLI

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A simple command line tool to upload files to a [FileSharer](https://github.com/DieHardcoreIT/filesharer) server.

Part of the [FileSharer](https://github.com/DieHardcoreIT/filesharer) project.


## Getting Started

1.  Configure the settings in the `appsettings.json` file.

```json
{
    "ApiSettings": {
        "BaseUrl": "http://localhost:3535",
        "ApiKey": "password"
    },
    "UploadSettings": {
      "ConcurrentUploads": 5
    }
}
```

2.  Run the application.
    ```bash
    filesharer-uploader.exe YourFile.zip
    ```


## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.