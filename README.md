> **1.0 개발판 3: [SOLIDWORKS 공식 자료를 참조한 단계별 스케치·쌓기·파기 안내](docs/SOLIDWORKS-WORKFLOW-REFERENCE.ko.md)**

> **1.0 개발판 2: [평면 → 스케치 → 보스 → 면 → 새 스케치 → 컷 흐름 수정](docs/CORE-WORKFLOW-1.0-preview2.ko.md)**

# 디엠아이(DMI) CAD — 1.0 개발판 1

![DMI](https://static.wixstatic.com/media/b61e9b_4c68109deaca422494f28c350edb38de~mv2.png)

Windows용 정밀 스케치·솔리드 모델링 개발판입니다. 정식 1.0 완성판이 아닙니다.

이번에는 **회전 보스/컷·전체 모서리 필렛·새 모델링 예제**를 묶었습니다.
기존 화면 면 선택·돌출 끝 평면 연결·관통 컷·피처 편집·저장 복원도 유지합니다.

## 바로 사용하기
- [1.0 개발판 사용법과 제한](docs/MODELING-1.0-preview1.ko.md)
- 검사/출력 → **1.0 금속 플레이트** 또는 **회전 부품**
- [필렛 플레이트 프로젝트](examples/plate-1.0-preview1.dmipart)
- [회전 중공 축 프로젝트](examples/turned-1.0-preview1.dmipart)
- [도움말 목록](docs/README.md)

## 설치
[Windows Actions](https://github.com/dmi20190118-tech/dmi-cad/actions)의 성공한 빌드 → Artifacts → DMI-CAD-Windows-x64에서 ZIP을 받고 압축을 풉니다.
DMI-CAD-Setup-1.0-preview3.exe 실행. Windows 10/11 x64, .NET 실행 환경과 CAD 엔진 포함. GitHub 로그인이 필요할 수 있습니다.
프로젝트 v5는 이전 앱에서 열리지 않습니다. v1~v4 프로젝트를 읽고 새 이름으로 저장할 수 있습니다.

## 범위
원·닫힌 다각형 스케치, 기준면 돌출/컷, 회전 형상, 전체 모서리 필렛, 유효한 단일 솔리드 및 STEP/IGES/STL 출력입니다.
정밀 계산은 Open CASCADE를 사용하고 화면은 면 번호가 연결된 표시 메시를 사용합니다.

일반 스케치 구속 솔버·호·정밀 모서리/꼭짓점 선택과 측정·선택 모서리 필렛·임의 평면·도면/조립·금형/CAM/NC는 미완료입니다.
PFM·SOLIDWORKS·CATIA·Cimatron·DWG 원본 읽기는 미지원입니다. 기본 DXF와 STEP/IGES/STL 뷰어는 유지합니다.
기존 모바일 페이지는 지시서/상자 체험이며 Windows CAD 엔진을 실행하지 않습니다.

## 검증·로드맵
자동 검사로 정확한 부피·형상 유효성·파일 변환·프로젝트 구조·실제 GUI 작업 흐름을 확인합니다.
전체 수동 마우스·DPI·사용자 환경·실제 생산 검증은 별도로 필요합니다.
- [1.0 핵심 합격 기준](docs/ACCEPTANCE-1.0.ko.md)
- [통합 개발 로드맵](PRODUCT-ROADMAP.md)
- [가공 지시서 범위](MACHINING-SHEETS.md)
- [라이선스 고지](THIRD-PARTY.md)
