> 최신 개발 버전: **0.3.2** — [돌출·컷 방향 반전 사용법](docs/MODELING-0.3.2.ko.md)

# 디엠아이(DMI) CAD 0.3.1

![DMI](https://static.wixstatic.com/media/b61e9b_4c68109deaca422494f28c350edb38de~mv2.png)

Windows용 정밀 스케치·솔리드 모델링 개발 버전입니다. Design · Mold · Injection의 통합 설계·제조 환경으로 확장하는 초기 단계입니다.

## 0.3.1 인터페이스
SOLIDWORKS 2020 공식 도움말의 기본 배치를 참고해 상단 작업별 명령 탭, 왼쪽 피처 트리/속성 전환과 확인/취소, 모델 위 보기 도구를 구성했습니다. 자체 DMI 로고·아이콘을 사용합니다. [새 사용 순서와 검사 범위](docs/INTERFACE-0.3.1.ko.md)

## 0.3 모델링의 핵심 작업
- 사각형·원·닫힌 다각형을 화면에서 그리거나 숫자 치수로 정의
- XY/XZ/YZ 평면과 오프셋·깊이로 정확한 솔리드 돌출
- 재료 추가와 잘라내기, 유효한 단일 솔리드와 실제 부피 변화 검사
- 피처 트리 선택·수정·삭제, 이후 피처 재계산
- 실행 취소/다시 실행, .dmipart 저장/열기
- 정확한 형상의 STEP/IGES 및 STL 출력
- 모델 이름·리비전·표시 메시 외형을 가공 지시서 초안에 전달
- 기존 STEP/IGES/STL/기본 DXF 뷰어와 지시서 창 유지

이는 일반 스케치 구속 솔버나 완성된 SOLIDWORKS 대체품이 아닙니다.
필렛·곡면 편집·정밀 면/모서리 선택·치수 측정·연동 도면·조립·금형·CAM/NC 생성은 아직 없습니다.
PFM·Cimatron·CATIA·SOLIDWORKS·DWG 원본 읽기도 미지원입니다.

## 바로 시작하기
[0.3 모델링 작업 안내](docs/MODELING-0.3.ko.md)

기본 순서: 사각형 → 좌표/너비/높이 입력 → 치수 적용 → 평면/깊이 설정 → 돌출 추가.
예제 만들기는 실제 엔진으로 플레이트·구멍·보스 3개 피처를 만듭니다.
[예제 프로젝트](examples/plate.dmipart)를 내려받아 열어볼 수도 있습니다.

## Windows 설치 파일
[Actions](https://github.com/dmi20190118-tech/dmi-cad/actions)의 성공한 0.3 빌드에서 Artifacts → DMI-CAD-Windows-x64를 받으세요.
ZIP을 풀고 DMI-CAD-Setup-0.3.1.exe를 실행합니다. Windows 10/11 x64, .NET과 CAD 엔진 포함.
코드 서명이 없는 초기 제품이며 실제 고객 모델·현장 작업에는 추가 검증이 필요합니다.

## 검증
빌드 과정에서 샘플 STEP/IGES 변환, 정밀 부피 계산, 돌출·빼기·피처 변경, STEP 재읽기, XY/XZ/YZ 평면, 자기교차·빈 절삭·분리 솔리드 거절, 프로젝트 복사·저장 구조, STL/DXF와 지시서 검사를 실행합니다.
실제 GUI에서 3피처 모델을 생성하고 화면을 캡처한 뒤 설치파일을 만듭니다.
수동 스케치 조작·실제 장비·모든 버전 파일 호환을 검증했다는 뜻은 아닙니다.

## 모바일
[기존 모바일 체험판](https://headless-wjqqtfyrafq-khigp012-1408.wix-site-host.com)은 지시서와 예제 상자 체험입니다.
이 모바일 페이지에서 Windows 0.3 정밀 CAD 엔진이 실행되지는 않습니다.

## 도움말과 방향
- [도움말 목록](docs/README.md)
- [통합 개발 로드맵](PRODUCT-ROADMAP.md)
- [가공 지시서](MACHINING-SHEETS.md)
- [라이선스 고지](THIRD-PARTY.md)
- [원본 DMI 브랜드](docs/BRAND.ko.md)

실제 Excel 양식·조건표와 NC/장비 샘플은 아직 확보되지 않았습니다. NC 자동 대조·자동 지시서·생산용 NC 출력은 제공하지 않습니다.
