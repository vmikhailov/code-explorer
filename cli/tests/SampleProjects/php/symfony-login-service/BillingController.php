<?php
namespace AppBundle\Controller;

use Symfony\Bundle\FrameworkBundle\Controller\Controller;
use Symfony\Component\Routing\Annotation\Route;
use Mreg\Request as MregRequest;

/**
 * @Route("/billing")
 */
class BillingController extends Controller
{
    /**
     * @Route("/pay", name="billing_pay", methods={"POST"})
     */
    public function payAction($invoiceItems, $conn, $smarty)
    {
        if (count($invoiceItems) > 0) {
            while (list($k, $v) = each($invoiceItems)) {
                $amount = round($v['price'], 2);
            }
        }

        $rows = $conn->fetchAll("SELECT id, total FROM customer_invoices WHERE status = 'OPEN' GROUP BY account_id");
        $conn->update('customer_payments', array('status' => 'PAID'), array('id' => 1));

        $mreg = new MregRequest();
        $mreg->sendCommand('PayCustomerInvoice');

        return $smarty->display('billing/receipt.tpl');
    }
}
