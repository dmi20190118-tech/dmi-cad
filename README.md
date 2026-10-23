# DMI CAD 0.1 — Windows 인터페이스 시제품

상단 명령 탭, 왼쪽 모델 트리, 가운데 작업 화면으로 구성한 독립적인 CAD 인터페이스 시제품입니다. SOLIDWORKS의 코드, 로고, 자산을 사용하지 않습니다.

## 구현된 동작
- 예제 직육면체 표시, 너비/깊이/높이 변경
- 가운데 버튼 드래그 회전, Ctrl+가운데 버튼 화면 이동
- Shift+가운데 버튼 확대/축소, 휠 아래로 확대·위로 축소
- 가운데 버튼 더블클릭 화면 맞춤
- 이후 조작 개발 기준: [마우스 호환 요구사항](MOUSE-CONTROLS.md)
- 등각/정면/평면 및 와이어프레임 보기
- 새 부품, 자체 .dmicad 파일 저장과 불러오기

선/곡면 편집, 실제 스케치 돌출, STEP/DWG 입출력, 정밀 CAD 형상 엔진, NC/CAM은 아직 없습니다. 해당 도구는 개발 예정 안내를 표시합니다.

## Windows 설치파일
[Actions](https://github.com/dmi20190118-tech/dmi-cad/actions) → Build Windows installer → 성공한 실행 → Artifacts → DMI-CAD-Windows-x64에서 ZIP을 받으세요.
압축을 풀고 DMI-CAD-Setup-0.1.0.exe를 실행하세요.
Windows 10/11 x64용이며 .NET 런타임을 포함합니다. 초기 시제품이며 코드 서명이 없습니다.

## 빌드
Windows에서 .NET 8 SDK로 다음 명령을 실행합니다.

```powershell
dotnet publish DmiCad.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Inno Setup 6에서 installer.iss를 컴파일하면 설치파일을 만듭니다.

GitHub Actions는 컴파일, 주 창 시작 확인, 설치파일 생성을 수행하도록 구성되어 있습니다. 전체 UI 조작과 설치/제거의 수동 검증은 별도로 필요합니다.
