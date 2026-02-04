unit UnitA;

interface

uses
  System.SysUtils,
  UnitB;

procedure DoSomething;

implementation

{$I constants.inc}

procedure DoSomething;
begin
  WriteLn('UnitA: DoSomething');
  UnitB.DoSomethingElse;
end;

end.
