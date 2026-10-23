# DMI CAD 0.2 — Windows CAD 파일 뷰어

Windows 10/11 x64에서 파일을 로컬로 읽고, 솔리드웍스 방식의 기본 마우스 조작으로 회전/이동/확대합니다. 파일은 서버로 업로드하지 않습니다.

## 지원 형식

| 형식 | 기능 / 제한 |
|---|---|
| STEP (.step/.stp) | Open CASCADE로 읽기, 삼각 메시 표시, STEP/IGES/STL 내보내기 |
| IGES (.iges/.igs) | Open CASCADE로 읽기, 삼각 메시 표시, STEP/IGES/STL 내보내기 |
| STL | ASCII/바이너리 읽기 및 바이너리 STL 내보내기 |
| DXF | ASCII LINE, ARC, CIRCLE, 직선 LWPOLYLINE 표시. 블록·텍스트·스플라인·불지·기울어진 OCS 등 미지원 요소가 있으면 오류로 중단 |
| .dmicad | 기존 예제 직육면체 프로젝트 |

SOLIDWORKS (.sldprt/.sldasm), CATIA (.CATPart/.CATProduct), Cimatron/Elite 원본, DWG는 직접 읽지 못합니다. 2025 등 특정 버전 호환도 보장하지 않습니다. 별도 상용 변환 SDK와 라이선스가 필요합니다. 원래 CAD에서 STEP/IGES/기본 2D DXF로 내보내서 여세요.

ISO가 NC G코드를 뜻한다면 별도의 가공 경로 기능이 필요합니다. ISO 10303 형식은 STEP으로 처리합니다.

읽은 모델은 표시용이며 편집 가능한 피처 이력을 복원하지 않습니다. 재질·조립 트리·PMI·레이어·텍스트 보존을 보장하지 않습니다. STEP↔IGES 변환 시에도 속성 손실 가능성이 있습니다. STL은 삼각 메시로 변환되어 정밀 곡면 정보가 사라집니다.

파일당 128 MB, 메시 25만 삼각형, 엔진 처리 3분으로 제한합니다. 대형 모델은 거친 메시/분할 파일로 내보내세요. 크기 표시는 파일 좌표 기준입니다. STL/DXF는 원래 내보내기 단위를 별도로 확인하세요. 오류가 나면 이전 모델을 유지합니다.

## 사용
파일 → 열기에서 모델을 선택합니다. 변환 / 내보내기에서 출력 형식을 고릅니다. DXF 선 도면을 STL 입체나 STEP 솔리드로 자동 변환하지 않습니다.

- 가운데 버튼 드래그: 회전
- Ctrl+가운데 버튼: 이동
- Shift+가운데 버튼: 확대/축소
- 휠 아래로 확대, 위로 축소 (포인터 기준)
- 가운데 버튼 더블클릭: 화면 맞춤

정밀 스케치/곡면 편집, CAM/NC 검증은 아직 없습니다.

## 설치
[GitHub Actions](https://github.com/dmi20190118-tech/dmi-cad/actions)에서 성공한 최신 실행의 Artifacts → DMI-CAD-Windows-x64를 받으세요.
ZIP을 풀고 DMI-CAD-Setup-0.2.0.exe를 실행합니다. .NET과 CAD 엔진이 포함되며 별도 Python 설치가 필요 없습니다. 설치파일은 코드 서명이 없는 시제품입니다.

## 검증
Windows CI에서 STL/DXF 입출력 검사, 번들 엔진의 STEP/IGES/STL 변환 검사, 창 시작 검사 후 설치파일을 생성합니다.
실제 고객 파일의 형상 정확도와 모든 UI 조작은 추가 검증이 필요합니다.

## 개발
.NET 8 Windows Forms, Python 3.11, cadquery-ocp 7.8 계열, PyInstaller를 사용합니다. 정확한 빌드 절차는 .github/workflows/windows.yml을 참고하세요. 배포 라이선스와 소스 위치는 THIRD-PARTY.md에 있습니다.
