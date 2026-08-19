.DEFAULT_GOAL := help
APP := $(HOME)/Applications/ClipDeck.app

help: ## Mostra esta ajuda
	@echo "ClipDeck — comandos:"
	@grep -E '^[a-z-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN{FS=":.*?## "};{printf "  \033[36m%-10s\033[0m %s\n", $$1, $$2}'
	@echo ""
	@echo "Permissão de Acessibilidade (necessária para o colar automático):"
	@echo "  Ajustes do Sistema > Privacidade e Segurança > Acessibilidade > + ClipDeck"
	@echo "  Se a permissão cair a cada rebuild, rode 'make cert' uma vez:"
	@echo "  ela dá ao app uma identidade estável e o problema acaba."

cert: ## Cria o certificado local (uma vez por máquina) para a permissão não cair
	@./scripts/make-cert.sh

build: ## Compila em release
	@swift build -c release 2>&1 | grep -v "could not determine XCTest" || true

test: ## Roda os testes (harness próprio; XCTest não existe sem Xcode)
	@swift run SelfTest 2>&1 | grep -v "could not determine XCTest" || true

bundle: build ## Monta o .app em build/
	@./scripts/bundle.sh

install: bundle ## Instala em ~/Applications
	@pkill -x ClipDeck 2>/dev/null || true
	@rm -rf "$(APP)"
	@mkdir -p "$(HOME)/Applications"
	@cp -R build/ClipDeck.app "$(APP)"
	@echo "✓ instalado em $(APP)"

run: install ## Instala e abre
	@open "$(APP)"
	@echo "✓ rodando — procure o ícone na barra de menu"

stop: ## Encerra o app
	@pkill -x ClipDeck 2>/dev/null && echo "✓ encerrado" || echo "não estava rodando"

clean: ## Remove artefatos de build
	@rm -rf .build build
	@echo "✓ limpo"

.PHONY: help cert build test bundle install run stop clean
