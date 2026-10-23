Option Explicit
Dim pdf, input, output, text, fields, bytes, identity, count
If WScript.Arguments.Count <> 2 Then WScript.Quit 2
input = WScript.Arguments(0)
output = WScript.Arguments(1)
Set pdf = CreateObject("Fluent.Pdf.Automation.1")
pdf.Configure 1048576, 16777216, 1048576, 100000, 30000
pdf.OpenFile input
If pdf.PageCount <> 2 Then WScript.Quit 3
text = pdf.ExtractText(0, 0, False, 1000)
If text <> "Original automation " & ChrW(&H3A9) Then WScript.Quit 4
fields = pdf.GetFieldNames()
If fields(0) <> "customer.Name" Then WScript.Quit 5
bytes = pdf.SaveBytes(False)
If UBound(bytes) < 100 Then WScript.Quit 6
pdf.Close
pdf.OpenBytes bytes
If pdf.PageCount <> 2 Then WScript.Quit 7
pdf.SelectPages "2,1", False
If pdf.ExtractText(0, 0, False, 1000) <> "Second automation page" Then WScript.Quit 8
pdf.SaveFile output, False
identity = pdf.RuntimeIdentity
pdf.Close
On Error Resume Next
count = pdf.PageCount
If Err.Number <> CLng(&H8004A104) Then WScript.Quit 9
If InStr(Err.Description, "Automation.NoDocument") = 0 Then WScript.Quit 10
Err.Clear
On Error GoTo 0
Set pdf = Nothing
WScript.Echo "COM_VBS_PASS;" & identity
