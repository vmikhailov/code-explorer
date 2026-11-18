<?php
namespace AppBundle\Entity;

use Doctrine\ORM\Mapping as ORM;

/**
 * @ORM\Entity
 * @ORM\Table(name="customer_invoices")
 */
class CustomerInvoice
{
    private $id;
    private $accountId;
    private $totalAmount;

    public function getId()
    {
        return $this->id;
    }

    public function getTotalAmount()
    {
        return $this->totalAmount;
    }
}
