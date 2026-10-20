# Independent Open XML source fixture check

This development-only executable uses the published DocumentFormat.OpenXml 3.3.0 validator (MIT license), with exact dependency identities in packages.lock.json. It is outside the product solution and introduces no SDK runtime dependency. It opens only controlled original PPTX/XLSX fixtures read-only, validates against the explicit Office2019 schema/part rules and writes a hashed report, retaining earlier reports by digest. Failures produce a nonzero process exit; success does not qualify complete format/application fidelity or final packages.

Run with `dotnet run --project eng/format-checks/OpenXmlCheck.csproj -- fixture.pptx report.json` after explicitly restoring the tool. The generated data profile uses literal strings, ISO calendar dates, merged ranges and PNG pictures. No imported actions, macros, external media or office applications execute.
