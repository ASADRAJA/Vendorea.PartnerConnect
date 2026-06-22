<?php
/**
 * SPR StockCheck / DealerStockCheck SOAP probe.
 *
 * Purpose: SPR's interactive services are SOAP 1.1 rpc/encoded (NuSOAP). Our hand-built C# client
 * works for QuickCheckPlus (individual params) but NOT for StockCheck/DealerStockCheck, whose single
 * request part is a STRUCT (`StockCheckInputs`). PHP's SoapClient is the reference rpc/encoded client
 * and serializes that struct the way the server expects. This script calls the services with tracing
 * ON and prints the exact request envelope it puts on the wire so we can mirror it in C#.
 *
 * Requires: PHP with the `soap` extension (php -m | grep soap).
 *
 * Usage:
 *   php stockcheck_soap_probe.php
 *   # or override any value via env:
 *   SPR_BASE=http://sprwstst.sprich.com/sprws \
 *   SPR_GROUP=0033822.00 SPR_USER=WebService SPR_PASS='<password>' \
 *   SPR_CUST=0033822.01 SPR_ITEM=EVEE91 \
 *   php stockcheck_soap_probe.php
 *
 * SPR_PASS is REQUIRED (no default) so no credential is committed to the repo.
 */

error_reporting(E_ALL & ~E_DEPRECATED);

$base  = getenv('SPR_BASE')  ?: 'http://sprwstst.sprich.com/sprws';
$group = getenv('SPR_GROUP') ?: '0033822.00';
$user  = getenv('SPR_USER')  ?: 'WebService';
$pass  = getenv('SPR_PASS')  ?: '';
$cust  = getenv('SPR_CUST')  ?: '0033822.01';
$item  = getenv('SPR_ITEM')  ?: 'EVEE91';

if ($pass === '') {
    fwrite(STDERR, "ERROR: set the SPR web-service password via the SPR_PASS environment variable.\n");
    exit(2);
}

if (!extension_loaded('soap')) {
    fwrite(STDERR, "ERROR: PHP 'soap' extension is not loaded. Install/enable it (e.g. `brew install php` ships with soap, or `apt install php-soap`).\n");
    exit(2);
}

// The struct members for StockCheckInputs / DealerStockCheckInputs (same shape).
// Action 'F' = fetch stock availability. DcNumber blank = all DCs. AvailableOnly 'N' = all DCs.
$input = [
    'GroupCode'      => $group,
    'UserID'         => $user,
    'Password'       => $pass,
    'Action'         => 'F',
    'CustNumber'     => $cust,
    'DcNumber'       => '',
    'ItemNumber'     => $item,
    'SortBy'         => 'A',
    'MinInFullPacks' => '',
    'AvailableOnly'  => 'N',
];

/**
 * Calls one service and dumps the on-the-wire request + the response.
 */
function probe(string $label, string $wsdl, string $operation, array $input): void
{
    echo str_repeat('=', 78), "\n";
    echo "## {$label}  ({$operation})\n";
    echo str_repeat('=', 78), "\n";

    try {
        $client = new SoapClient($wsdl, [
            'trace'        => 1,          // capture __getLastRequest()/__getLastResponse()
            'exceptions'   => true,
            'soap_version' => SOAP_1_1,
            'cache_wsdl'   => WSDL_CACHE_NONE,
            'connection_timeout' => 30,
        ]);
    } catch (Throwable $e) {
        echo "WSDL load failed: {$e->getMessage()}\n\n";
        return;
    }

    $result = null;
    try {
        // rpc/encoded: the single part 'input' is a struct. Pass it positionally.
        $result = $client->__soapCall($operation, [$input]);
    } catch (Throwable $e) {
        echo "SOAP call threw: {$e->getMessage()}\n";
    }

    echo "\n--- REQUEST ENVELOPE (mirror THIS in the C# builder) ---\n";
    echo prettyXml($client->__getLastRequest()), "\n";

    echo "--- RESPONSE ENVELOPE ---\n";
    echo prettyXml($client->__getLastResponse()), "\n";

    echo "--- PARSED RESULT ---\n";
    var_export($result);
    echo "\n\n";
}

function prettyXml(?string $xml): string
{
    if (!$xml) return '(empty)';
    $dom = new DOMDocument('1.0');
    $dom->preserveWhiteSpace = false;
    $dom->formatOutput = true;
    if (@$dom->loadXML($xml)) {
        return $dom->saveXML();
    }
    return $xml;
}

probe('StockCheck (no price, all DCs)',        "{$base}/StockCheck.php?wsdl",       'StockCheck',       $input);
probe('DealerStockCheck (price + all DCs)',    "{$base}/DealerStockCheck.php?wsdl", 'DealerStockCheck', $input);

echo "Done. Copy the REQUEST ENVELOPE block(s) back so the C# envelope can be matched exactly.\n";
