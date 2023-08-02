**Attention!** _This article, as well as this announcement, are automatically translated from Russian_.

The **Net.Leksi.E6dWebApp** library (E6dWebApp is short for Embedded Web Application) allows you to embed a web service (hereinafter _server_) into a local application for various application purposes. For example:
- Generating text files using Razor Pages:
     + for example, sources of various stubs and auxiliary files, as, for example, happens in WPF,
     + reports from the desktop application in the form of web pages,
     + something else...,
- Unit testing of the web service.

All classes are contained in the `Net.Leksi.E6dWebApp` namespace.

* `Runner` - a class that controls the configuration, start and stop of _server_, and also provides authorized access to it.
* `IConnector` - interface of the object provided by `Runner` through which authorized requests to _server_ are made.
* `RequestParameter` - a carrier object of a user object transmitted to the _server_ in parallel with the request. This object is available on _server_ through dependency injection.

**Important**: It is recommended to create any project that uses this library as an **Empty ASP.Net Core template**, or manually replace the `Sdk` attribute from "Microsoft.NET.Sdk" to "Microsoft.NET.Sdk.Web" in the project's XML file!

It is also proposed to familiarize yourself with the demonstration projects:
- `Demo:Helloer` - shows how to use the `GetLink` connector method.
- `Demo:InterfaceImplementer` - shows how to use the built-in web server to generate class source files.
- `Demo:UnitTesting` - shows how to write unit tests for a web application.

