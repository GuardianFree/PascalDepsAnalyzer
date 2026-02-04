unit SharedUnit;

interface

uses
  System.SysUtils;

procedure SharedFunction;

implementation

procedure SharedFunction;
begin
  WriteLn('Shared function from parent directory');
end;

end.
