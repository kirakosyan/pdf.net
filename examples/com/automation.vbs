Option Explicit
Dim pdf, input, output, text, fields, bytes, identity, count
If WScript.Arguments.Count <> 2 Then WScript.Quit 2
input = WScript.Arguments(0)
output = WScript.Arguments(1)
WScript.Echo "COM_VBS_STAGE;activate"
Set pdf = CreateObject("Fluent.Pdf.Automation.1")
WScript.Echo "COM_VBS_STAGE;configure"
pdf.Configure 1048576, 16777216, 1048576, 100000, 30000
WScript.Echo "COM_VBS_STAGE;open-file"
pdf.OpenFile input
If pdf.PageCount <> 2 Then WScript.Quit 3
WScript.Echo "COM_VBS_STAGE;extract-text"
text = pdf.ExtractText(0, 0, False, 1000)
If text <> "Original automation " & ChrW(&H3A9) Then WScript.Quit 4
WScript.Echo "COM_VBS_STAGE;field-array"
fields = pdf.GetFieldNames()
If fields(0) <> "customer.Name" Then WScript.Quit 5
WScript.Echo "COM_VBS_STAGE;save-array"
bytes = pdf.SaveBytes(False)
If UBound(bytes) < 100 Then WScript.Quit 6
pdf.Close
WScript.Echo "COM_VBS_STAGE;open-array"
pdf.OpenBytes bytes
If pdf.PageCount <> 2 Then WScript.Quit 7
WScript.Echo "COM_VBS_STAGE;select-pages"
pdf.SelectPages "2,1", False
If pdf.ExtractText(0, 0, False, 1000) <> "Second automation page" Then WScript.Quit 8
WScript.Echo "COM_VBS_STAGE;save-file"
pdf.SaveFile output, False
identity = pdf.RuntimeIdentity
pdf.Close
WScript.Echo "COM_VBS_STAGE;error-info"
On Error Resume Next
count = pdf.PageCount
If Err.Number <> CLng(&H8004A104) Then WScript.Quit 9
If InStr(Err.Description, "Automation.NoDocument") = 0 Then WScript.Quit 10
Err.Clear
On Error GoTo 0
Set pdf = Nothing
WScript.Echo "COM_VBS_PASS;" & identity
