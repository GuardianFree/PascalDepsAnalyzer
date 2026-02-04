unit UnitB;

interface

uses
  System.SysUtils,
  UnitC;

procedure DoSomethingElse;
procedure RunTest;

implementation

procedure DoSomethingElse;
begin
  WriteLn('UnitB: DoSomethingElse');
  UnitC.Helper;
end;

procedure RunTest;
begin
  WriteLn('Running test...');
end;

end.
