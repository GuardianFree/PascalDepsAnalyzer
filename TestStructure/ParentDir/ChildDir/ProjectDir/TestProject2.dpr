program TestProject2;

{$APPTYPE CONSOLE}

uses
  System.SysUtils,
  SharedUnit,      // должен найтись в ParentDir (2 уровня вверх)
  DeepUnit;        // должен найтись в SubDir1/SubDir2/SubDir3 (3 уровня вниз)

begin
  try
    WriteLn('Test Application with deep and parent search');
    SharedFunction;
    DeepFunction;
  except
    on E: Exception do
      Writeln(E.ClassName, ': ', E.Message);
  end;
end.
