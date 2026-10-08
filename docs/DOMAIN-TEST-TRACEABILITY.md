# Domain implementation and rule traceability

The Domain project has no package or project references. It contains the Claim and ClaimReserveComponent aggregate roots, immutable financial ledger identity/deltas, child entities, value objects, enums, pure rules, domain events, and business exceptions. The existing xUnit 2.9.3 and Shouldly 4.3.0 test dependencies are reused without changing package pins.

## Changed files

All 17 Domain source files below are new, under `backend/src/ClaimsModule.Domain/`:

| Directory | Files |
| --- | --- |
| Common | AggregateRoot.cs; IClock.cs; RuleGuard.cs |
| Entities | Claim.cs; ClaimParty.cs; ClaimReserveComponent.cs; ClaimRiskObject.cs; ClaimValidationIssue.cs; LossEvent.cs; ReserveHistory.cs |
| Enums | ClaimEnums.cs |
| Events | DomainEvents.cs |
| Exceptions | DomainRuleViolationException.cs |
| Rules | ClaimStateMachine.cs; ReserveAuthorityPolicy.cs |
| ValueObjects | ClaimNumber.cs; PolicyAssociation.cs |

Six new files under `backend/tests/ClaimsModule.Domain.Tests/`: ClaimStateMachineTests.cs, ClaimTests.cs, DomainFixture.cs, ReserveAuthorityPolicyTests.cs, ReserveComponentTests.cs, and ValueObjectAndEntityTests.cs. Existing scaffold files and project/package configuration are unchanged.

Documentation changes: DESIGN-DECISIONS.md records the user clarifications; this DOMAIN-TEST-TRACEABILITY.md is new.

## Domain boundaries

- IDs and allocated claim numbers are supplied by callers. ClaimNumber only validates/formats the value; allocation, uniqueness, gap-free counters, rowversion updates, tenant identity, and soft deletion belong to Persistence.
- D1 describes one loss event per claim; Claim exposes that single LossEvent alongside read-only Parties, RiskObjects and ValidationIssues collections.
- Every operation requiring time receives IClock. Claim.LastActivity is a business marker (time and revision) which a future Persistence interceptor maps to shadow UpdatedAt. It is not an independently persisted audit column. SLA detection sets SlaBreachedAt without changing that marker or status.
- ReserveOperationContext must contain the owning Claim and **all** its component aggregates, loaded and protected by the same owning transaction. Domain checks claim identity, duplicate component types, and inclusion of the affected aggregate. Loading a complete set and serializing concurrent approvals remain Application/Persistence responsibilities; Domain cannot discover an omitted persisted component.
- The state machine defaults to D1's 12 edges. A supplied reference-table snapshot is used for edge/RequiredRole validation (DD-11); all condition checks remain Domain rules. Liability determination is an explicit context input, per the user clarification in DESIGN-DECISIONS.md.
- PreviousBalance/NewBalance are unset for unapproved transactions. Both are calculated when a transaction joins the approved ledger, so they reflect approval order rather than submission sequence. CurrentAmount is the approved-only ledger cache; posting success/failure does not affect it.
- Posting methods report whether they changed state. Duplicate success, duplicate terminal failure, and stale failure after success return false without creating new activity. Durable enqueueing, database idempotency, audit insertion, and retry authorization are outside Domain.

## D1 BR/CC coverage

Test names below are discoverable in `backend/tests/ClaimsModule.Domain.Tests`. Every requested BR-R, BR-ST, BR-P and CC rule has a named test. Matrix expectations are authored independently from the production transition/authority/sign logic.

| Rule | Named tests | Test file |
| --- | --- | --- |
| BR-R-01 | BRR01DD04AllComponentTransactionSigns; BRR01AutoApprovalCannotMakeBalanceNegative; BRR01ApprovalRechecksBalanceAfterInterveningChanges | ReserveComponentTests.cs |
| BR-R-02 | BRR02ExactThresholdBoundariesUseAbsoluteDelta; BRR02RoleAuthorityMatrix; BRR02AnyRoleMaySubmitAboveItsAuthority; BRR02ThresholdUsesDeltaRatherThanRunningTotal; BRR02PendingAndCancelledTransactionsCannotBePosted | ReserveAuthorityPolicyTests.cs; ReserveComponentTests.cs |
| BR-R-03 | BRR03SelfApprovalThrowsDistinctException | ReserveComponentTests.cs |
| BR-R-04 | BRR04ResubmissionLinksRetainedRejectedRowAndUsesNormalThreshold; BRR04ResubmissionMustLinkSameComponentRejectedRowAndUseAddOrAdjust | ReserveComponentTests.cs |
| BR-R-05 | BRR05CapForcesSmallDeltaPendingAndBlocksUntilManagerOverride; BRR05CapUsesNetSignedBalancesAcrossAllComponentsAndIgnoresPending; BRR05ApprovalRechecksAggregateCapAfterAnotherComponentIsApproved | ReserveComponentTests.cs |
| BR-R-06 | BRR06IdempotencyKeyUsesComponentIdAndSequencesNeverReuseRejectedOrCancelledNumbers; BRR06PostingIsIdempotentAndPostingStateDoesNotAffectBalance | ReserveComponentTests.cs |
| BR-ST-01 | BRST01FullStateMachineMatrixMatchesD1; BRST01InvalidTransitionListsValidNextStatuses; BRST01InvalidTransitionDoesNotMutateClaimOrEmitEvents | ClaimStateMachineTests.cs; ClaimTests.cs |
| BR-ST-02 | BRST02AllOpenEntriesRequireResolvedCriticalIssuesClaimantAndHandler; BRST02OpenWarningsRequireExplicitAcknowledgement; BRST02LaterCriticalIssuesMustBeResolvedAndCannotBeAcknowledged | ClaimStateMachineTests.cs; ClaimTests.cs |
| BR-ST-03 | BRST03ClosureReturnsAllBlockingConditions; BRST03ClosureBlocksPendingApprovalUntilItIsRetracted | ClaimStateMachineTests.cs; ClaimTests.cs |
| BR-ST-04 | BRST04ReopenRequiresSupervisorAndReason; BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened; BRST04ReopenValidatesOpenLegBeforeAnyMutation | ClaimStateMachineTests.cs; ClaimTests.cs |
| BR-P-01 | BRP01LastActiveClaimantCannotBeRemovedEvenInDraft; BRST02AllOpenEntriesRequireResolvedCriticalIssuesClaimantAndHandler | ClaimTests.cs; ClaimStateMachineTests.cs |
| BR-P-02 | BRP02AllRolesAndRepeatedRolesAreSupported | ClaimTests.cs |
| CC-01 | CC01PendingApprovalBlocksClosure; BRST03ClosureBlocksPendingApprovalUntilItIsRetracted | ClaimStateMachineTests.cs; ClaimTests.cs |
| CC-02 | CC02UnresolvedCriticalIssuesBlockClosure; CC02ClaimClosureRemainsBlockedUntilLaterCriticalIssueIsResolved | ClaimStateMachineTests.cs; ClaimTests.cs |
| CC-03 | CC03ClosureRequiresActiveClaimant | ClaimStateMachineTests.cs |
| CC-04 | CC04NonZeroReserveRequiresJustificationAndReturnsWarning; CC04ClosureRequiresJustificationForPositiveOrNegativeBalance | ClaimStateMachineTests.cs; ClaimTests.cs |

BR-R-06 coverage here proves Domain key/sequence and posting-state behavior. A future integration test must prove job/database/audit idempotency; no such infrastructure is introduced by this task.

## Additional functional and decision coverage

| Rule/decision | Named tests |
| --- | --- |
| BR-C-01 | BRC01LossDateUsesInjectedClockAndRejectsFutureOrMissingDate |
| BR-C-02 | BRC02LinkPolicyReevaluatesPeriodWarningsAndResolvesUnknownPolicy |
| BR-C-03, DD-10 clarification | DD10ClaimantIsMandatoryAtCreation |
| BR-C-04, DD-16 format only | BRC04ClaimNumberFormatsParsesAndComparesByValue; BRC04ClaimNumberRejectsOutOfRangeYearOrSequence; BRC04ClaimNumberRejectsMalformedFormat |
| BR-C-05 | BRC05InactiveOrUnknownCauseRejectsLossEvent (catalogue lookup is supplied by Application) |
| BR-C-06 | BRC06UnknownPolicyBlocksSubmissionUntilLinked |
| BR-C-07 | BRC07LossDescriptionRequiresTwentyCharacters |
| DD-05, DD-28 | DD05DD28ApprovedOnlyLedgerCacheAndApprovalOrderBalances; DD28DuplicateComponentAndWrongClaimContextAreRejected |
| DD-06 | Workflow assertions retain rejected amounts and posting transaction amount/key; no public financial mutation API exists |
| DD-07, DD-08 clarification | DD07OverrideDoesNotLiftIndividualTenMillionCeiling |
| DD-07 retraction | DD07RetractOnlyBySubmitterWhilePendingCancelsPosting; DD07RetractCannotCancelAutoOrManuallyApprovedTransactions |
| DD-10 | DD10CriticalIssueRejectsCreation; DD10WarningsPersistAndOpenAcknowledgesThem; BRST02LaterCriticalIssuesMustBeResolvedAndCannotBeAcknowledged |
| DD-11 | DD11EveryValidTransitionHasRequiredRole; PersistedTransitionTableMaySupplyHigherRequiredRole; ManagerOverrideEmitsEventAndAssignmentRequiresSupervisor |
| DD-12 | BRST01InvalidTransitionListsValidNextStatuses; BRST03ClosureReturnsAllBlockingConditions |
| DD-13 | BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened |
| DD-14 | WithdrawnIsTerminalAndRequiresReason; BRST01FullStateMachineMatrixMatchesD1; CC04ClosureRequiresJustificationForPositiveOrNegativeBalance |
| DD-18, posting clarification | DD18TouchAndBusinessActionsUseClockAndClearSlaWithoutChangingStatus; BRR06PostingIsIdempotentAndPostingStateDoesNotAffectBalance |
| DD-27 | BRR04ResubmissionLinksRetainedRejectedRowAndUsesNormalThreshold |
| DD-30 | CreateSetsDraftDefaultsAndEmitsClaimCreated; DD30EstimatedLossCannotBeNegativeButMayBeZeroOrAbsent |
| Parties/risk objects | PartyTypeEnforcesPersonVersusCompanyFieldRules; CompanyRequiresOnlyCompanyName; RiskObjectSupportsEveryAssetTypeAndRequiresDescription |
| Events | CreateSetsDraftDefaultsAndEmitsClaimCreated; BRP01LastActiveClaimantCannotBeRemovedEvenInDraft; DD10WarningsPersistAndOpenAcknowledgesThem; BRC02LinkPolicyReevaluatesPeriodWarningsAndResolvesUnknownPolicy; CC04ClosureRequiresJustificationForPositiveOrNegativeBalance; BRST04ReopenAtomicallyEndsOpenAndEmitsTwoStatusChangesAndClaimReopened; ManagerOverrideEmitsEventAndAssignmentRequiresSupervisor; ReserveWorkflowEmitsAllRequestedEventsAndCanClearThem |
| Dependency isolation | DomainHasNoExternalOrOtherProjectAssemblyDependencies |

## Verification

The repository's global.json accepts SDK 9.0.318, installed locally in its existing ignored `.dotnet` path. No global SDK configuration or package versions were changed. In this environment use `.dotnet/dotnet.exe` with `DOTNET_ROOT` pointing to that directory; single-process MSBuild and disabled compiler sharing avoid the compiler-process cancellation observed with the default build command.

```powershell
$env:DOTNET_ROOT = 'C:/Test/DICEUS_FullStack/.dotnet'
& ./.dotnet/dotnet.exe build backend/ClaimsModule.sln --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
& ./.dotnet/dotnet.exe test backend/tests/ClaimsModule.Domain.Tests/ClaimsModule.Domain.Tests.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
```

The whole solution build reports an existing NU1903 advisory for AutoMapper 14.0.0 in Application. Domain introduces no packages. Package remediation is outside this Domain task and must respect the repository's AutoMapper <=14.x policy.

Final verification on 2026-10-04: solution build succeeded (zero errors, the single existing NU1903 warning). The complete solution test run passed **260/260** cases: **253 Domain**, **1 Application**, **6 Integration**, with no failures or skipped tests. TRX results are under the ignored TestResults directory. Domain cases include all 49 status pairs, the 12 required-role edges, 36 sign combinations, and the authority/threshold boundary matrices. A documentation check confirmed that every mapped BR/CC test name exists; `git diff --check` reported no whitespace errors.
