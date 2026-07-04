# [1.0.0](https://github.com/Natteens/intellipool/compare/v0.1.3...v1.0.0) (2026-07-03)

### BREAKING CHANGES

* Runtime rewritten on top of UnityEngine.Pool.ObjectPool<T>. Old static Pool API (SpawnByTag/Despawn/DespawnDelayed), PoolContainer and PoolableObject removed. New API: Pool.Get / Pool.Release / Pool.ReleaseDelayed delegating to a PoolService instance.
* PoolDatabase reshaped: PoolConfiguration[] pools -> List<PoolEntry> entries (id, prefab, prewarmCount, defaultCapacity, maxSize, collectionCheck, clearOnSceneLoad, containerName). enablePoolSystem, useJobsForBatching and jobsThreshold removed.
* IMGUI PoolSetupWindow/PoolManagerWindow replaced by a single UI Toolkit Pool Manager window.
* EditorPrefs database path removed; default database is loaded from Resources/IntelliPool/PoolDatabase or set manually via Pool.Initialize / PoolService.

## [0.1.3](https://github.com/Natteens/intellipool/compare/v0.1.2...v0.1.3) (2025-08-21)


### Bug Fixes

* Update semantic-release plugins configuration ([b24a7f6](https://github.com/Natteens/intellipool/commit/b24a7f6ab69e983016a7db4f3893a88380093939))

## [0.1.2](https://github.com/Natteens/intellipool/compare/v0.1.1...v0.1.2) (2025-08-20)


### Bug Fixes

* Add UNITY_EDITOR guards to editor-specific code ([c077465](https://github.com/Natteens/intellipool/commit/c0774652539cb10963b4737c94fc88c704b18453))

## [0.1.1](https://github.com/Natteens/intellipool/compare/v0.1.0...v0.1.1) (2025-08-11)


### Bug Fixes

* Replace PoolDatabaseEditor with new PoolManagerWindow ([3e8e015](https://github.com/Natteens/intellipool/commit/3e8e015d865e046fd14d053299d2be0dc59a3ac2))

# 📝 Changelog

Todas as mudanças notáveis neste projeto serão documentadas neste arquivo.

O formato é baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/),
e este projeto adere ao [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Não Lançado]

## [0.1.0] - 2025-07-03

### Adicionado
- ✨ Estrutura inicial do pacote Unity
- 📦 Configuração do Package Manager
- 📚 Documentação básica
- 🧪 Estrutura de testes
- 📋 Exemplos e amostras

### Mudado
- Nada ainda

### Removido
- Nada ainda

### Corrigido
- Nada ainda

---

Os tipos de mudanças são:
- **Adicionado** para novas funcionalidades
- **Mudado** para mudanças em funcionalidades existentes
- **Depreciado** para funcionalidades que serão removidas em breve
- **Removido** para funcionalidades removidas
- **Corrigido** para correções de bugs
- **Segurança** para vulnerabilidades
