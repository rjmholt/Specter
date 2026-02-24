# Copyright (c) Microsoft Corporation. All rights reserved.
# Licensed under the MIT License.

BeforeAll {
    $violationName = "PSAvoidUsingInvokeExpression"
    $violations = Invoke-ScriptAnalyzer $PSScriptRoot\AvoidUsingInvokeExpression.ps1 | Where-Object {$_.RuleName -eq $violationName}
    $noViolations = Invoke-ScriptAnalyzer $PSScriptRoot\AvoidConvertToSecureStringWithPlainTextNoViolations.ps1 | Where-Object {$_.RuleName -eq $violationName}
}

Describe "AvoidUsingInvokeExpression" {
    Context "When there are violations" {
        It "has 2 Avoid Using Invoke-Expression violations" {
            $violations.Count | Should -Be 2
        }

        It "has the correct description message" {
            $violations[0].Message | Should -Match 'Invoke-Expression is used'
        }
    }

    Context "When there are no violations" {
        It "returns no violations" {
            $noViolations.Count | Should -Be 0
        }
    }
}
