program TestProject;

{$APPTYPE CONSOLE}

uses
  System.SysUtils,
  UnitA in 'UnitA.pas',
  UnitB in 'UnitB.pas';

begin
  try
    WriteLn('Test Application');
    RunTest;
  except
    on E: Exception do
      Writeln(E.ClassName, ': ', E.Message);
  end;
end.
